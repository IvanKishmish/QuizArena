namespace TelegramBot.Domain.Entities;

public sealed class BroadcastLog
{
    public Guid Id { get; set; }
    public long InitiatedByTelegramUserId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public int TargetCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
