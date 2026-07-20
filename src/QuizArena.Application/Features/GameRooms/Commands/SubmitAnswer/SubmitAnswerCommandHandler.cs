using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using FluentValidation;
using QuizArena.Application.Common.Interfaces.Leaderboard;
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
    public async ValueTask<ErrorOr<int>> Handle(SubmitAnswerCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);

        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");
        
        if(gameRoom.Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Game is not currently in progress.");

        var participant = gameRoom.Participants
            .FirstOrDefault(p => p.Id == command.ParticipantId);
        
        if (participant is null)
            return Error.NotFound("Participant.NotFound", "Participant not found in this room.");

        var question = await questionStore.GetByIdAsync(gameRoom.QuizSetId, command.QuestionId, ct);
        
        if (question is null)
            return Error.NotFound("Question.NotFound", "Question not found.");

        var elapsedSeconds = gameRoom.CurrentQuestionStartedAt is null
            ? question.TimeLimitSeconds
            : (DateTimeOffset.UtcNow - gameRoom.CurrentQuestionStartedAt.Value).TotalSeconds;

        var score = question.CalculateScore(command.SelectedOptionIndices, elapsedSeconds);

        var addScoreResult = participant.AddScore(score);

        if (addScoreResult.IsError)
            return addScoreResult.Errors;
        
        await gameRoomStore.SaveAsync(gameRoom, ct);

        await leaderboardStore.UpdateScoreAsync(
            command.RoomCode, participant.Id, participant.DisplayName, participant.Score, ct);

        var topCount = Math.Min(gameRoom.Participants.Count, 5);
        var topEntries = await leaderboardStore.GetTopAsync(command.RoomCode, topCount, ct);
        
        await gameNotifier.LeaderboardUpdatedAsync(command.RoomCode, topEntries, ct);

        return score;
    }
}