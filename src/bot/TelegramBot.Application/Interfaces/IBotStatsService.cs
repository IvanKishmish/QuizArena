namespace TelegramBot.Application.Interfaces;

public sealed record BotStatsSnapshot(
    long TotalUniqueUsersEver,
    long ActiveGameSessionsNow,
    long MessagesProcessedToday);

public interface IBotStatsService
{
    Task RecordMessageProcessedAsync(long chatId, CancellationToken ct);
    Task<BotStatsSnapshot> GetSnapshotAsync(CancellationToken ct);
}

public interface IBroadcastService
{
    Task<int> GetRecipientCountAsync(CancellationToken ct);

    /// <summary>Sends to every known chat, skipping ones the user blocked the bot in
    /// (Telegram returns 403 for those — recorded as failures, not thrown).</summary>
    Task<(int success, int failure)> BroadcastAsync(long initiatedByTelegramUserId, string text, CancellationToken ct);
}
