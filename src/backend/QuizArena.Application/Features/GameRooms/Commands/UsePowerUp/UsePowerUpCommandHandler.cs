using ErrorOr;
using FluentValidation;
using Mediator;
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
    
    public async ValueTask<ErrorOr<Updated>> Handle(UsePowerUpCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);
        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");

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

        object notificationPayload = command.PowerUpType switch
        {
            PowerUpType.Freeze => ApplyFreeze(target!, command),
            PowerUpType.FiftyFifty => await ApplyFiftyFifty(gameRoom, command, ct),
            PowerUpType.DoubleOrNothing => new { command.ParticipantId, Effect = "DoubleOrNothing" },
            _ => throw new ArgumentOutOfRangeException(nameof(command.PowerUpType))
        };

        await gameRoomStore.SaveAsync(gameRoom, ct);
        await gameNotifier.PowerUpUsedAsync(command.RoomCode, notificationPayload, ct);

        return Result.Updated;
    }

    private static object ApplyFreeze(Participant target, UsePowerUpCommand command)
    {
        target.ApplyFreeze(FreezeDuration);

        return new
        {
            command.ParticipantId,
            Effect = "Freeze",
            Target = command.TargetParticipantId,
            DurationSeconds = FreezeDuration.TotalSeconds
        };
    }
    
    private async Task<object> ApplyFiftyFifty(GameRoom gameRoom, UsePowerUpCommand command, CancellationToken ct = default)
    {
        var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
        var currentQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex);

        if (currentQuestion is null)
            return new { command.ParticipantId, Effect = "FiftyFifty", EliminatedCount = 0 };

        var wrongIndices = currentQuestion.Options
            .Select((o, i) => (o.IsCorrect, Index: i))
            .Where(x => !x.IsCorrect)
            .Select(x => x.Index)
            .ToList();

        var eliminateCount = Math.Max(0, Math.Min(2, wrongIndices.Count - 1));

        var eliminatedIndices = wrongIndices
            .OrderBy(_ => Random.Shared.Next())
            .Take(eliminateCount)
            .ToList();

        var connectionId = await connectionTracker.GetConnectionAsync(command.ParticipantId, ct);

        if (connectionId is not null)
            await gameNotifier.SendToParticipantAsync(
                connectionId,
                "FiftyFiftyApplied",
                new { QuestionId = currentQuestion.Id, EliminatedOptionIndices = eliminatedIndices },
                ct);

        return new { command.ParticipantId, Effect = "FiftyFifty", EliminatedCount = eliminatedIndices.Count };
    }
}