namespace TelegramBot.Domain.Entities;

/// <summary>
/// A Telegram account allowed to run the bot's own admin commands
/// (/admin_stats, /admin_broadcast, /admin_maintenance_on|off).
/// Deliberately unrelated to the QuizArena backend's "Admin" role/claim —
/// this only controls the bot process itself.
/// </summary>
public sealed class BotAdmin
{
    public long TelegramUserId { get; set; }

    public string? TelegramUsername { get; set; }

    public DateTimeOffset GrantedAtUtc { get; set; }

    public string? GrantedBy { get; set; }
}
