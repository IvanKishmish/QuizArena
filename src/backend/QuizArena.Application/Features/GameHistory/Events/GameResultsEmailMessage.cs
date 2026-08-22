namespace QuizArena.Application.Features.GameHistory.Events;

public sealed record GameResultsEmailMessage(string Email, string DisplayName, int Score, int Placement, bool IsWinner);
