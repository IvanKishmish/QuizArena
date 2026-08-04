using ErrorOr;
using FluentValidation;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Benchmarking.H1;

// ВАЖЛИВО: тіло цього методу дослівно повторює SubmitAnswerCommandHandler.Handle()
// з Features/GameRooms/Commands/SubmitAnswer. Єдина змінна, яку H1 тестує — це
// механізм диспетчеризації (Mediator / MediatR / Immediate.Handlers), не бізнес-логіка.
public static class SubmitAnswerCore
{
    public static async ValueTask<ErrorOr<int>> ExecuteAsync(
        SubmitAnswerInput input,
        IGameRoomStore gameRoomStore,
        IQuestionStore questionStore,
        ILeaderboardStore leaderboardStore,
        IGameNotifier gameNotifier,
        IValidator<SubmitAnswerInput> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(input, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(input.RoomCode, ct);

        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");

        if (gameRoom.Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Game is not currently in progress.");

        var participant = gameRoom.Participants
            .FirstOrDefault(p => p.Id == input.ParticipantId);

        if (participant is null)
            return Error.NotFound("Participant.NotFound", "Participant not found in this room.");

        if (participant.IsFrozen)
        {
            participant.ClearFreezeIfExpired();

            if (participant.IsFrozen)
                return Error.Validation("Participant.Frozen", "You are currently frozen and cannot answer.");
        }

        var question = await questionStore.GetByIdAsync(gameRoom.QuizSetId, input.QuestionId, ct);

        if (question is null)
            return Error.NotFound("Question.NotFound", "Question not found.");

        var elapsedSeconds = gameRoom.CurrentQuestionStartedAt is null
            ? question.TimeLimitSeconds
            : (DateTimeOffset.UtcNow - gameRoom.CurrentQuestionStartedAt.Value).TotalSeconds;

        var score = question.CalculateScore(input.SelectedOptionIndices, elapsedSeconds);

        var addScoreResult = participant.AddScore(score);

        if (addScoreResult.IsError)
            return addScoreResult.Errors;

        await gameRoomStore.SaveAsync(gameRoom, ct);

        await leaderboardStore.UpdateScoreAsync(
            input.RoomCode, participant.Id, participant.DisplayName, participant.Score, ct);

        var topCount = Math.Min(gameRoom.Participants.Count, 5);
        var topEntries = await leaderboardStore.GetTopAsync(input.RoomCode, topCount, ct);

        await gameNotifier.LeaderboardUpdatedAsync(input.RoomCode, topEntries, ct);

        return score;
    }
}
