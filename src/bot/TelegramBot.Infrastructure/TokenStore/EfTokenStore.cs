using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TelegramBot.Application.Interfaces;
using TelegramBot.Domain.Entities;
using TelegramBot.Infrastructure.Persistence;

namespace TelegramBot.Infrastructure.TokenStore;

public sealed class EfTokenStore : ITokenStore
{
    private const string Purpose = "TelegramBot.TokenStore.v1";

    private readonly BotDbContext _db;
    private readonly IDataProtector _protector;

    public EfTokenStore(BotDbContext db, IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public async Task<BotUserSession?> GetAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        return session;
    }

    /// <summary>Returns the same entity as GetAsync, but with tokens decrypted — used only
    /// internally by the API client, never exposed to the Bot presentation layer.</summary>
    internal async Task<(string? accessToken, DateTimeOffset? accessExpiry, string? refreshCookie, DateTimeOffset? refreshExpiry)>
        GetDecryptedTokensAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (session is null) return (null, null, null, null);

        var access = session.AccessTokenEncrypted is null ? null : Unprotect(session.AccessTokenEncrypted);
        var refresh = session.RefreshTokenCookieEncrypted is null ? null : Unprotect(session.RefreshTokenCookieEncrypted);
        return (access, session.AccessTokenExpiresAt, refresh, session.RefreshTokenExpiresAt);
    }

    public async Task EnsureSessionAsync(long chatId, long telegramUserId, string? telegramUsername, CancellationToken ct)
    {
        var existing = await _db.UserSessions.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (existing is null)
        {
            _db.UserSessions.Add(new BotUserSession
            {
                ChatId = chatId,
                TelegramUserId = telegramUserId,
                TelegramUsername = telegramUsername,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.TelegramUsername = telegramUsername;
            existing.LastSeenAtUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveAccessTokenAsync(long chatId, string accessToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var session = await GetOrThrowAsync(chatId, ct);
        session.AccessTokenEncrypted = Protect(accessToken);
        session.AccessTokenExpiresAt = expiresAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveTokenPairAsync(
        long chatId,
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string refreshTokenCookieValue,
        DateTimeOffset refreshTokenExpiresAt,
        CancellationToken ct)
    {
        var session = await GetOrThrowAsync(chatId, ct);
        session.AccessTokenEncrypted = Protect(accessToken);
        session.AccessTokenExpiresAt = accessTokenExpiresAt;
        session.RefreshTokenCookieEncrypted = Protect(refreshTokenCookieValue);
        session.RefreshTokenExpiresAt = refreshTokenExpiresAt;
        session.ConsecutiveAuthFailures = 0;
        session.IsFlaggedAsPossiblyBanned = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (session is null) return;

        session.AccessTokenEncrypted = null;
        session.AccessTokenExpiresAt = null;
        session.RefreshTokenCookieEncrypted = null;
        session.RefreshTokenExpiresAt = null;
        session.QuizArenaUserId = null;
        session.NickName = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordAuthFailureAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (session is null) return;

        session.ConsecutiveAuthFailures++;
        // Threshold chosen deliberately low: a couple of genuine 401s can happen from an
        // access token expiring mid-flight, but 403 (Forbidden, i.e. authenticated-but-blocked)
        // is the real "banned" signal and callers should flag on the first one — see
        // QuizArenaApiClient, which calls this only after ruling out a simple expiry.
        if (session.ConsecutiveAuthFailures >= 2)
            session.IsFlaggedAsPossiblyBanned = true;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordAuthSuccessAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (session is null) return;

        session.ConsecutiveAuthFailures = 0;
        session.IsFlaggedAsPossiblyBanned = false;
        session.LastSeenAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<BotUserSession> GetOrThrowAsync(long chatId, CancellationToken ct)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (session is null)
            throw new InvalidOperationException(
                $"No session row for chat {chatId}. Call EnsureSessionAsync before saving tokens.");
        return session;
    }

    private string Protect(string plain) => _protector.Protect(plain);
    private string Unprotect(string cipher) => _protector.Unprotect(cipher);
}
