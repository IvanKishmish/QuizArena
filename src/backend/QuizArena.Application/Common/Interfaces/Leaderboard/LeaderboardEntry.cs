namespace QuizArena.Application.Common.Interfaces.Leaderboard;

public sealed record LeaderboardEntry(Guid ParticipantId, string DisplayName, double Score);