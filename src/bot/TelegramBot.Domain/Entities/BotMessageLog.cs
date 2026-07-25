namespace TelegramBot.Domain.Entities;

public sealed class BotMessageLog
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public long TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public string? NickName { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
}
