namespace TelegramBot.Application.Contracts.Api;

public enum QuizVisibility
{
    Private = 0,
    Public = 1
}

public sealed record AdminUserSummary(Guid Id, string Email, string Nickname, bool IsBanned, DateTimeOffset RegisteredAt);

public sealed record AdminQuizSetSummary(Guid Id, string Title, Guid OwnerId, QuizVisibility Visibility, DateTimeOffset CreatedAt);

public sealed record AdminDashboardStats(int TotalUsers, int TotalQuizSets, int TotalPublishedQuizSets, int TotalGamesPlayed);
