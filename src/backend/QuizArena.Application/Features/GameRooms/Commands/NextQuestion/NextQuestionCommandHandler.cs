using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Features.GameRooms.Commands.NextQuestion;

public sealed class NextQuestionCommandHandler(
    IGameRoomStore gameRoomStore,
    IQuestionStore questionStore,
    IGameNotifier gameNotifier,
    IValidator<NextQuestionCommand> validator)
: ICommandHandler<NextQuestionCommand, ErrorOr<Updated>>
{
    private sealed record NextQuestionOutcome(Question CurrentQuestion);

    public async ValueTask<ErrorOr<Updated>> Handle(NextQuestionCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var result = await OptimisticConcurrency.ExecuteAsync(
            gameRoomStore, command.RoomCode, (gameRoom, innerCt) => MutateAsync(gameRoom, innerCt), ct);

        if (result.IsError)
            return result.Errors;

        var currentQuestion = result.Value.CurrentQuestion;

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

    private async Task<ErrorOr<NextQuestionOutcome>> MutateAsync(GameRoom gameRoom, CancellationToken ct)
    {
        var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
        var nextQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex + 1);

        if (nextQuestion is null)
            return Error.NotFound("Question.NotFound", "No more questions in this quiz.");

        var nextResult = gameRoom.NextQuestion();

        if (nextResult.IsError)
            return nextResult.Errors;

        return new NextQuestionOutcome(nextQuestion);
    }
}
