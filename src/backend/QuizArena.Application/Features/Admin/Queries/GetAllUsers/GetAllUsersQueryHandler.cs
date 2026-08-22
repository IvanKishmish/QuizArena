using Mediator;
using ErrorOr;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.IdentityQueryService;

namespace QuizArena.Application.Features.Admin.Queries.GetAllUsers;

public sealed class GetAllUsersQueryHandler(
    IIdentityUserQueryService identityUserQueryService,
    IAppDbContext context,
    IValidator<GetAllUsersQuery> validator)
    : IQueryHandler<GetAllUsersQuery, ErrorOr<PagedResponse<UserSummary>>>
{
    public async ValueTask<ErrorOr<PagedResponse<UserSummary>>> Handle(
        GetAllUsersQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var (users, totalCount) = await identityUserQueryService.GetPagedUsersAsync(
            query.PageNumber, query.PageSize, ct);

        var userIds = users.Select(u => u.Id).ToList();

        var nicknamesByUserId = await context.Players
            .AsNoTracking()
            .Where(p => userIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NickName })
            .ToDictionaryAsync(p => p.Id, p => p.NickName, ct);

        var items = users
            .Select(u => new UserSummary(
                u.Id,
                u.Email,
                nicknamesByUserId.GetValueOrDefault(u.Id, "—"),
                u.IsBanned,
                u.RegisteredAt))
            .ToList();

        return new PagedResponse<UserSummary>(items, query.PageNumber, query.PageSize, totalCount);
    }
}