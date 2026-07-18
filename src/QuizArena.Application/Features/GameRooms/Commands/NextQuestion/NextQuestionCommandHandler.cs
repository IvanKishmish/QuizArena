using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.GameRooms.Commands.NextQuestion;

public sealed class NextQuestionCommandHandler(
    IGameRoomStore gameRoomStore,
    IQuestionStore questionStore,
    IGameNotifier gameNotifier)
: ICommandHandler<NextQuestionCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(NextQuestionCommand command, CancellationToken ct = default)
    {
        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);
        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");
        
        var nextResult = gameRoom.NextQuestion();
        if (nextResult.IsError)
            return nextResult.Errors;
        
        await gameRoomStore.SaveAsync(gameRoom, ct);

        var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
        var currentQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex);

        if (currentQuestion is null)
            return Error.NotFound("Question.NotFound", "No more questions.");

        var payload = new
        {
            currentQuestion.Id,
            currentQuestion.Text,
            currentQuestion.QuestionType,
            currentQuestion.TimeLimitSeconds,
            Options = currentQuestion.Options.Select(o => new { o.Text, o.OrderIndex })
        };
        
        await gameNotifier.QuestionStartedAsync(command.RoomCode, payload, ct);

        return Result.Updated;
    }
}