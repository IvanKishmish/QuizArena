using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Admin.Commands.DeleteAnyQuizSet;

public sealed class DeleteAnyQuizSetCommandHandler(IAppDbContext context, IQuestionStore questionStore)
: ICommandHandler<DeleteAnyQuizSetCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(DeleteAnyQuizSetCommand command, CancellationToken ct = default)
    {
        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);
        
        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        context.QuizSets.Remove(quizSet);
        
        await questionStore.DeleteByQuizSetIdAsync(quizSet.Id, ct);
        
        await context.SaveChangesAsync(ct);

        return Result.Deleted;
    }
}