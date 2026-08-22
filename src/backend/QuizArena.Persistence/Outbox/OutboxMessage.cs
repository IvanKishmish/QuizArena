namespace QuizArena.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage()
    { } // ef

    public static OutboxMessage Create(string type, string payload) => new()
    {
        Type = type,
        Payload = payload
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;

    public void MarkFailed(string error, TimeSpan retryDelay)
    {
        RetryCount++;
        LastError = error.Length > 2000 ? error[..2000] : error; // don't let a huge stack trace bloat the row
        NextAttemptAt = DateTimeOffset.UtcNow.Add(retryDelay);
    }
}
