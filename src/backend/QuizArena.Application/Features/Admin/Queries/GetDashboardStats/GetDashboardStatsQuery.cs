using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Admin.Queries.GetDashboardStats;

public sealed record DashboardStats(
    int TotalUsers,
    int TotalQuizSets,
    int TotalPublishedQuizSets,
    int TotalGamesPlayed);
    
public sealed record GetDashboardStatsQuery : IQuery<ErrorOr<DashboardStats>>;