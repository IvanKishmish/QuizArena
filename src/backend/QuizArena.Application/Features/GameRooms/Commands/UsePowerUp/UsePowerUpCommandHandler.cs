using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;

public sealed class UsePowerUpCommandHandler(
    IGameRoomStore gameRoomStore,
    IGameNotifier gameNotifier,
    IConnectionTracker connectionTracker,
    IQuestionStore questionStore,
    IValidator<UsePowerUpCommand> validator)
: ICommandHandler<UsePowerUpCommand, ErrorOr<Updated>>
{
    private static readonly TimeSpan FreezeDuration = TimeSpan.FromSeconds(3);

    private sealed record UsePowerUpOutcome(
        PowerUpType PowerUpType,
        Guid ParticipantId,
        Guid? TargetParticipantId,
        Guid? CurrentQuestionId,
        List<int> EliminatedOptionIndices);

    public async ValueTask<ErrorOr<Updated>> Handle(UsePowerUpCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var result = await OptimisticConcurrency.ExecuteAsync(
            gameRoomStore, command.RoomCode, (gameRoom, innerCt) => MutateAsync(gameRoom, command, innerCt), ct);

        if (result.IsError)
            return result.Errors;

        var outcome = result.Value;

        object notificationPayload = outcome.PowerUpType switch
        {
            PowerUpType.Freeze => new
            {
                outcome.ParticipantId,
                Effect = "Freeze",
                Target = outcome.TargetParticipantId,
                DurationSeconds = FreezeDuration.TotalSeconds
            },
            PowerUpType.FiftyFifty => new
            {
                outcome.ParticipantId,
                Effect = "FiftyFifty",
                EliminatedCount = outcome.EliminatedOptionIndices.Count
            },
            PowerUpType.DoubleOrNothing => new { outcome.ParticipantId, Effect = "DoubleOrNothing" },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.PowerUpType))
        };

        await gameNotifier.PowerUpUsedAsync(command.RoomCode, notificationPayload, ct);

        if (outcome is { PowerUpType: PowerUpType.FiftyFifty, CurrentQuestionId: not null } &&
            outcome.EliminatedOptionIndices.Count > 0)
        {
            var connectionId = await connectionTracker.GetConnectionAsync(outcome.ParticipantId, ct);

            if (connectionId is not null)
                await gameNotifier.SendToParticipantAsync(
                    connectionId,
                    "FiftyFiftyApplied",
                    new { QuestionId = outcome.CurrentQuestionId, EliminatedOptionIndices = outcome.EliminatedOptionIndices },
                    ct);
        }

        return Result.Updated;
    }

    private async Task<ErrorOr<UsePowerUpOutcome>> MutateAsync(
        GameRoom gameRoom, UsePowerUpCommand command, CancellationToken ct)
    {
        if (gameRoom.Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Game is not currently in progress.");

        var participant = gameRoom.Participants.FirstOrDefault(p => p.Id == command.ParticipantId);
        if (participant is null)
            return Error.NotFound("Participant.NotFound", "Participant not found.");

        Participant? target = null;

        if (command.PowerUpType == PowerUpType.Freeze)
        {
            if (command.TargetParticipantId is null)
                return Error.Validation("PowerUp.TargetRequired", "Freeze requires a target participant.");

            target = gameRoom.Participants.FirstOrDefault(p => p.Id == command.TargetParticipantId);
            if (target is null)
                return Error.NotFound("Participant.NotFound", "Target participant not found.");

            if (target.Id == participant.Id)
                return Error.Validation("PowerUp.CannotTargetSelf", "You cannot freeze yourself.");
        }

        var useResult = participant.UsePowerUp(command.PowerUpType);
        if (useResult.IsError)
            return useResult.Errors;

        Guid? currentQuestionId = null;
        List<int> eliminatedIndices = [];

        switch (command.PowerUpType)
        {
            case PowerUpType.Freeze:
                target!.ApplyFreeze(FreezeDuration);
                break;

            case PowerUpType.FiftyFifty:
            {
                var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
                var currentQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex);

                if (currentQuestion is not null)
                {
                    currentQuestionId = currentQuestion.Id;

                    var wrongIndices = currentQuestion.Options
                        .Select((o, i) => (o.IsCorrect, Index: i))
                        .Where(x => !x.IsCorrect)
                        .Select(x => x.Index)
                        .ToList();

                    var eliminateCount = Math.Max(0, Math.Min(2, wrongIndices.Count - 1));

                    eliminatedIndices = wrongIndices
                        .OrderBy(_ => Random.Shared.Next())
                        .Take(eliminateCount)
                        .ToList();
                }

                break;
            }
        }

        return new UsePowerUpOutcome(
            command.PowerUpType, participant.Id, command.TargetParticipantId, currentQuestionId, eliminatedIndices);
    }
}
