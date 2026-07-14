using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.QuizSets.Commands.PublishQuizSet;

public sealed class PublishQuizSetCommandHandler
(
    IAppDbContext dbContext, 
    ICurrentUserService currentUser,
    IQuestionStore questionStore)
: ICommandHandler<PublishQuizSetCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(PublishQuizSetCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var quizSet = await dbContext.QuizSets.FindAsync([command.QuizSetId], ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");

        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var questions = await questionStore.GetByQuizSetIdAsync(command.QuizSetId, ct);

        if (questions.Count == 0)
            return Error.Validation("QuizSet.CannotPublishEmpty", "A quiz set must contain at least one question to be published.");

        var publishResult = quizSet.Publish();

        if (publishResult.IsError)
            return publishResult.Errors;

        await dbContext.SaveChangesAsync(ct);

        return Result.Updated;
    }
}