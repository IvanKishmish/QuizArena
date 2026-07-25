namespace TelegramBot.Application.Contracts.Api;

public sealed record RegisterRequest(string NickName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// What the bot ends up with after register/login/refresh: the JSON access token,
/// plus the raw Set-Cookie value for "refresh_token" that we captured ourselves
/// (the API layer never exposes this to callers above it).
/// </summary>
public sealed record TokenPairResult(string AccessToken, string RefreshTokenCookieValue, DateTimeOffset RefreshTokenExpiresAt);
