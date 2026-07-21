using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Commands.DeleteQuizSet;

public sealed class DeleteQuizSetCommandHandler
(IAppDbContext context,
    ICurrentUserService currentUser)
: ICommandHandler<DeleteQuizSetCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(DeleteQuizSetCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        if(quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        if (quizSet.Visibility == Visibility.Public)
            return Error.Validation("QuizSet.NotAvailableToDelete", "You can't delete the public quiz set.");
        
        context.QuizSets.Remove(quizSet);
        
        await context.SaveChangesAsync(ct);

        return Result.Deleted;
    }
}