namespace QuizArena.Persistence.Redis.Documents;

public sealed record ParticipantSnapshot
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public required Guid? UserId { get; init; }
    public required Guid? GuestId { get; init; }
    public required int Score { get; init; }
}