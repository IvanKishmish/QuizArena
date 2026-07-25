namespace TelegramBot.Domain.Entities;

/// <summary>
/// One row per calendar day (UTC). Incremented as updates flow through the pipeline.
/// Kept intentionally simple (counters, not events) so /admin_stats stays a single cheap
/// row read instead of an aggregation query over a growing events table.
/// </summary>
public sealed class BotUsageStat
{
    public DateOnly DateUtc { get; set; }
    public long MessagesProcessed { get; set; }
    public long UniqueChatsSeen { get; set; }
}
