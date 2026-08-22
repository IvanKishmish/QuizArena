using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Application.Features.GameHistory.Commands.SaveGameHistory;

public sealed record SaveGameHistoryCommand(
    Guid GameId,
    Guid QuizSetId,
    IReadOnlyList<LeaderboardEntry> FinalLeaderboard,
    IReadOnlyDictionary<Guid, Guid?> ParticipantUserIds) : ICommand<ErrorOr<Success>>;
