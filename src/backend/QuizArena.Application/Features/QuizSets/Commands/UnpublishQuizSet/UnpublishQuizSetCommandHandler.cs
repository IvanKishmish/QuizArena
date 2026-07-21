using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.QuizSets.Commands.UnpublishQuizSet;

public sealed class UnpublishQuizSetCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser)
: ICommandHandler<UnpublishQuizSetCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(UnpublishQuizSetCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        var quizSet = await dbContext.QuizSets.FindAsync([command.QuizSetId], ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");

        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var publishResult = quizSet.Unpublish();

        if (publishResult.IsError)
            return publishResult.Errors;
        
        await dbContext.SaveChangesAsync(ct);

        return Result.Updated;
    }
}