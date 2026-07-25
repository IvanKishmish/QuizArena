namespace TelegramBot.Bot.Options;

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;

    /// <summary>"Webhook" (production default) or "Polling" (local dev, no public URL needed).</summary>
    public string Mode { get; set; } = "Webhook";

    public string? WebhookPublicUrl { get; set; }

    public string? WebhookSecretToken { get; set; }
}

public sealed class BotAdminsOptions
{
    /// <summary>Telegram user ids granted admin on first boot if the bot_admins table is empty —
    /// a bootstrap mechanism so there's always at least one admin without manual DB surgery.</summary>
    public long[] SeedTelegramUserIds { get; set; } = [];
}

public sealed class RateLimitingOptions
{
    public int MaxCommandsPerMinutePerChat { get; set; } = 20;
}
