namespace TelegramBot.Domain.Entities;

/// <summary>
/// One row per Telegram chat that has ever authenticated against the QuizArena API.
/// Holds the QuizArena identity of that chat plus the encrypted tokens needed to act on
/// its behalf, so that a chat "stays logged in" across bot restarts.
/// </summary>
public sealed class BotUserSession
{
    /// <summary>Telegram chat id — the natural key for a bot conversation.</summary>
    public long ChatId { get; set; }

    /// <summary>Telegram user id (may differ from ChatId in groups; we only support private chats).</summary>
    public long TelegramUserId { get; set; }

    public string? TelegramUsername { get; set; }

    /// <summary>QuizArena user id, once known (from the JWT "sub" claim after login/register).</summary>
    public Guid? QuizArenaUserId { get; set; }

    public string? NickName { get; set; }

    /// <summary>Current short-lived access token, encrypted at rest via IDataProtector.</summary>
    public string? AccessTokenEncrypted { get; set; }

    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// The value of the "refresh_token" cookie the API issued us, encrypted at rest.
    /// The bot is not a browser, so it cannot rely on an automatic cookie jar — this is
    /// where we manually persist and replay that cookie. See ADR in README for details.
    /// </summary>
    public string? RefreshTokenCookieEncrypted { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>
    /// Consecutive count of 401/403 responses from the QuizArena API for this session.
    /// Used to detect "this account was probably banned by a backend admin" without
    /// leaking raw error text to the user. Reset to 0 on any successful authorized call.
    /// </summary>
    public int ConsecutiveAuthFailures { get; set; }

    public bool IsFlaggedAsPossiblyBanned { get; set; }
}
