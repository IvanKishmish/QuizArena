namespace QuizArena.Application.Common.Interfaces.IdentityQueryService;

public sealed record IdentityUserSummary(Guid Id, string Email, bool IsBanned, DateTimeOffset RegisteredAt);