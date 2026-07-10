using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;

public sealed class GetQuizSetByIdQueryHandler(
    IAppDbContext dbContext)
: IQueryHandler<GetQuizSetByIdQuery, ErrorOr<QuizSetResponse>>
{
    public async ValueTask<ErrorOr<QuizSetResponse>> Handle(GetQuizSetByIdQuery query, CancellationToken ct = default)
    {
        var quizSet = await dbContext.QuizSets
            .AsNoTracking().FirstOrDefaultAsync(q => q.Id == query.QuizSetId, ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        return new QuizSetResponse(
            quizSet.Id,
            quizSet.OwnerId,
            quizSet.Title,
            quizSet.Description,
            quizSet.Visibility.ToString());
    }
}