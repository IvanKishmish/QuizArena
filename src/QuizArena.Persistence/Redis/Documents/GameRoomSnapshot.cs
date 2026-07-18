using QuizArena.Domain.Enums;

namespace QuizArena.Persistence.Redis.Documents;

public sealed record GameRoomSnapshot
{
    public required Guid Id { get; init; }
    public required string RoomCode { get; init; }
    public required Guid QuizSetId { get; init; }
    public required Guid HostId { get; init; }
    public required GameRoomStatus Status { get; init; }
    public required int CurrentQuestionIndex { get; init; }
    public required DateTimeOffset? CurrentQuestionStartedAt { get; init; }
    public required List<ParticipantSnapshot> Participants { get; init; }
    public required DateTimeOffset? StartedAt { get; init; }
    public required DateTimeOffset? FinishedAt { get; init; }
}