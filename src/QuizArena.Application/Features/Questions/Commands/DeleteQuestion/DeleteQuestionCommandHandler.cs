using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Questions.Commands.DeleteQuestion;

public sealed class DeleteQuestionCommandHandler(
    IAppDbContext context,
    IQuestionStore questionStore,
    ICurrentUserService currentUser)
    : ICommandHandler<DeleteQuestionCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(DeleteQuestionCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);
        
        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var question = await questionStore.GetByIdAsync(command.QuizSetId, command.QuestionId, ct);

        if (question is null)
            return Error.NotFound("Question.NotFound", "Question not found.");

        await questionStore.DeleteAsync(command.QuizSetId, command.QuestionId, ct);

        return Result.Deleted;
    }
}