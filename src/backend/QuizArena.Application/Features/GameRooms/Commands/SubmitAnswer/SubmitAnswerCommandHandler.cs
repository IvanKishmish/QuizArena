using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using FluentValidation;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;

public sealed class SubmitAnswerCommandHandler(
    IGameRoomStore gameRoomStore,
    IQuestionStore questionStore,
    ILeaderboardStore leaderboardStore,
    IGameNotifier gameNotifier,
    IValidator<SubmitAnswerCommand> validator)
: ICommandHandler<SubmitAnswerCommand, ErrorOr<int>>
{
    private sealed record SubmitAnswerOutcome(int Score, string DisplayName, int TotalScore, int ParticipantCount);

    public async ValueTask<ErrorOr<int>> Handle(SubmitAnswerCommand command, CancellationToken ct = default)
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

        await leaderboardStore.UpdateScoreAsync(
            command.RoomCode, command.ParticipantId, outcome.DisplayName, outcome.TotalScore, ct);

        var topCount = Math.Min(outcome.ParticipantCount, 5);
        var topEntries = await leaderboardStore.GetTopAsync(command.RoomCode, topCount, ct);

        await gameNotifier.LeaderboardUpdatedAsync(command.RoomCode, topEntries, ct);

        return outcome.Score;
    }

    private async Task<ErrorOr<SubmitAnswerOutcome>> MutateAsync(
        GameRoom gameRoom, SubmitAnswerCommand command, CancellationToken ct)
    {
        if (gameRoom.Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Game is not currently in progress.");

        var participant = gameRoom.Participants.FirstOrDefault(p => p.Id == command.ParticipantId);

        if (participant is null)
            return Error.NotFound("Participant.NotFound", "Participant not found in this room.");

        if (participant.IsFrozen)
        {
            participant.ClearFreezeIfExpired();

            if (participant.IsFrozen)
                return Error.Validation("Participant.Frozen", "You are currently frozen and cannot answer.");
        }

        var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
        var currentQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex);

        if (currentQuestion is null)
            return Error.NotFound("Question.NotFound", "There is no active question right now.");

        if (currentQuestion.Id != command.QuestionId)
            return Error.Validation("Question.NotCurrent", "This is not the room's current question.");

        var elapsedSeconds = gameRoom.CurrentQuestionStartedAt is null
            ? currentQuestion.TimeLimitSeconds
            : (DateTimeOffset.UtcNow - gameRoom.CurrentQuestionStartedAt.Value).TotalSeconds;

        var score = currentQuestion.CalculateScore(command.SelectedOptionIndices, elapsedSeconds);

        var submitResult = participant.SubmitAnswer(command.QuestionId, score);

        if (submitResult.IsError)
            return submitResult.Errors;

        return new SubmitAnswerOutcome(score, participant.DisplayName, participant.Score, gameRoom.Participants.Count);
    }
}
