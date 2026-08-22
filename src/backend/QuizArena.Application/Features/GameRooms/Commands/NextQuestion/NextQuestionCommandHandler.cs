using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Enums;

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
        // Read-only status check first — must not mutate CurrentQuestionIndex before we know a next
        // question actually exists, otherwise a "no more questions" response would still leave the room
        // state advanced by one (visible in-memory even though nothing gets persisted, since SaveAsync
        // is never reached on an error result).
        if (gameRoom.Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Cannot advance questions when the game is not in progress.");

        var questions = await questionStore.GetByQuizSetIdAsync(gameRoom.QuizSetId, ct);
        var nextQuestion = questions.ElementAtOrDefault(gameRoom.CurrentQuestionIndex + 1);

        if (nextQuestion is null)
            return Error.NotFound("Question.NotFound", "No more questions in this quiz.");

        // Only now actually advance the index — NextQuestion() re-validates status defensively, which is
        // fine and cheap, but the important part is it can no longer fire on a "no more questions" path.
        var nextResult = gameRoom.NextQuestion();

        if (nextResult.IsError)
            return nextResult.Errors;

        return new NextQuestionOutcome(nextQuestion);
    }
}