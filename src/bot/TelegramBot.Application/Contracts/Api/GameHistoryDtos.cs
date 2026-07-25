namespace TelegramBot.Application.Contracts.Api;

public sealed record GameHistorySummary(Guid QuizSetId, string QuizSetTitle, int FinalScore, int Placement, DateTimeOffset PlayedAt);
