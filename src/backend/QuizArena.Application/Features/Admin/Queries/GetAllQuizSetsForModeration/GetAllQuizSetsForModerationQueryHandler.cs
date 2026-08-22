using Mediator;
using Microsoft.EntityFrameworkCore;
using ErrorOr;
using FluentValidation;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Admin.Queries.GetAllQuizSetsForModeration;

public sealed class GetAllQuizSetsForModerationQueryHandler(
    IAppDbContext dbContext, IValidator<GetAllQuizSetsForModerationQuery> validator)
    : IQueryHandler<GetAllQuizSetsForModerationQuery, ErrorOr<PagedResponse<AdminQuizSetSummary>>>
{
    public async ValueTask<ErrorOr<PagedResponse<AdminQuizSetSummary>>> Handle(
        GetAllQuizSetsForModerationQuery request, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var query = dbContext.QuizSets.AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(qs => qs.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(qs => new AdminQuizSetSummary(qs.Id, qs.Title, qs.OwnerId, qs.Visibility, qs.CreatedAt))
            .ToListAsync(ct);

        return new PagedResponse<AdminQuizSetSummary>(items, request.PageNumber, request.PageSize, totalCount);
    }
}