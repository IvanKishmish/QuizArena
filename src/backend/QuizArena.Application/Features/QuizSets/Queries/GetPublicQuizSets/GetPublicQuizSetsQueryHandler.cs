using ErrorOr;
using Mediator;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Queries.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

public sealed class GetPublicQuizSetsQueryHandler
(IAppDbContext dbContext, IValidator<GetPublicQuizSetsQuery> validator)
: IQueryHandler<GetPublicQuizSetsQuery, ErrorOr<PagedResponse<QuizSetSummary>>>
{
    public async ValueTask<ErrorOr<PagedResponse<QuizSetSummary>>> Handle(GetPublicQuizSetsQuery request,
        CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var query = dbContext.QuizSets
            .AsNoTracking()
            .Where(qs => qs.Visibility == Visibility.Public);
        
        var totalCount = await query.CountAsync(ct);
        
        var items = await query
            .OrderByDescending(qs => qs.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(qs => new QuizSetSummary(qs.Id, qs.Title, qs.Description, qs.Visibility))
            .ToListAsync(ct);
        
        return new PagedResponse<QuizSetSummary>(items, request.PageNumber, request.PageSize, totalCount);
    }
}