using Mediator;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.Admin.Queries.GetDashboardStats;

public sealed class GetDashboardStatsQueryHandler(IAppDbContext context)
: IQueryHandler<GetDashboardStatsQuery, ErrorOr<DashboardStats>>
{
    public async ValueTask<ErrorOr<DashboardStats>> Handle(GetDashboardStatsQuery query, CancellationToken ct = default)
    {
        var totalUsers = await context.Players.CountAsync(ct);
        var totalQuizSets = await context.QuizSets.CountAsync(ct);
        var totalPublished = await context.QuizSets.CountAsync(qs => qs.Visibility == Visibility.Public, ct);
        var totalGames = await context.GameHistory.Select(g => g.Id).Distinct().CountAsync(ct);

        return new DashboardStats(totalUsers, totalQuizSets, totalPublished, totalGames);
    }
}