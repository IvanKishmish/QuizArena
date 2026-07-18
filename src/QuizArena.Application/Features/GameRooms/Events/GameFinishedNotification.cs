using Mediator;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Application.Features.GameRooms.Events;

public sealed record GameFinishedNotification(
    string RoomCode,
    Guid QuizSetId,
    IReadOnlyList<LeaderboardEntry> FinalLeaderboard,
    IReadOnlyDictionary<Guid, Guid?> ParticipantUserIds) : INotification;