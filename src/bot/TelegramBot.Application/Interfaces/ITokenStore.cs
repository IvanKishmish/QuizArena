using TelegramBot.Domain.Entities;

namespace TelegramBot.Application.Interfaces;

/// <summary>
/// Persists and retrieves the QuizArena tokens bound to a Telegram chat.
/// Implementation (Infrastructure) is responsible for encrypting values at rest
/// via IDataProtector before they ever touch the database.
/// </summary>
public interface ITokenStore
{
    Task<BotUserSession?> GetAsync(long chatId, CancellationToken ct);

    Task SaveAccessTokenAsync(long chatId, string accessToken, DateTimeOffset expiresAt, CancellationToken ct);

    Task SaveTokenPairAsync(
        long chatId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshTokenCookieValue,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken ct);

    Task EnsureSessionAsync(long chatId, long telegramUserId, string? telegramUsername, CancellationToken ct);

    Task ClearAsync(long chatId, CancellationToken ct);

    Task RecordAuthFailureAsync(long chatId, CancellationToken ct);

    Task RecordAuthSuccessAsync(long chatId, CancellationToken ct);
}
