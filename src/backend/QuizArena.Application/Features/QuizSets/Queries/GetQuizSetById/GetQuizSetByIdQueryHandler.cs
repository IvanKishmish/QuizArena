using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;

public sealed class GetQuizSetByIdQueryHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser)
: IQueryHandler<GetQuizSetByIdQuery, ErrorOr<QuizSetResponse>>
{
    public async ValueTask<ErrorOr<QuizSetResponse>> Handle(GetQuizSetByIdQuery query, CancellationToken ct = default)
    {
        var quizSet = await dbContext.QuizSets
            .AsNoTracking().FirstOrDefaultAsync(q => q.Id == query.QuizSetId, ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        var isOwner = currentUser.UserId is not null && quizSet.OwnerId == currentUser.UserId;
        
        if(quizSet.Visibility != Visibility.Public && !isOwner)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        return new QuizSetResponse(
            quizSet.Id,
            quizSet.OwnerId,
            quizSet.Title,
            quizSet.Description,
            quizSet.Visibility.ToString());
    }
}