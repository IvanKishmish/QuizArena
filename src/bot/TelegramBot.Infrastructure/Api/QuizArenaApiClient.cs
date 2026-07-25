using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TelegramBot.Application.Common;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Interfaces;
using TelegramBot.Infrastructure.TokenStore;

namespace TelegramBot.Infrastructure.Api;

/// <summary>
/// See README "ADR: refresh token cookie for a non-browser client" for the full
/// reasoning. Short version: /api/auth/refresh only reads the "refresh_token" cookie
/// from the request — there is no JSON alternative today. A plain HttpClient has no
/// cookie jar of its own (by design — one HttpClient is shared across every chat, so a
/// CookieContainer would leak one user's cookie into another user's request). Instead,
/// on register/login/refresh we read the raw Set-Cookie header ourselves, store its
/// value encrypted per chatId (EfTokenStore), and manually attach it as a "Cookie"
/// request header on the next /refresh call for that chat. SameSite/Secure attributes
/// are a browser-enforced concept — replaying the cookie value server-to-server like
/// this is standard practice for non-browser clients and requires no backend change.
/// If the QuizArena backend later adds a JSON-body refresh endpoint for API clients,
/// this class is the only place that needs to change.
/// </summary>
public sealed class QuizArenaApiClient : IQuizArenaApiClient
{
    private const string RefreshCookieName = "refresh_token";

    private readonly HttpClient _http;
    private readonly EfTokenStore _tokenStore; // internal decrypt access
    private readonly ITokenStore _tokenStorePublic;
    private readonly ILogger<QuizArenaApiClient> _logger;

    public QuizArenaApiClient(HttpClient http, EfTokenStore tokenStore, ILogger<QuizArenaApiClient> logger)
    {
        _http = http;
        _tokenStore = tokenStore;
        _tokenStorePublic = tokenStore;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Auth

    public async Task<ApiResult<TokenPairResult>> RegisterAsync(long chatId, RegisterRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("/api/auth/register", request, ct);
        return await HandleAuthResponseAsync(chatId, response, ct);
    }

    public async Task<ApiResult<TokenPairResult>> LoginAsync(long chatId, LoginRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("/api/auth/login", request, ct);
        return await HandleAuthResponseAsync(chatId, response, ct);
    }

    public async Task<ApiResult<TokenPairResult>> RefreshAsync(long chatId, CancellationToken ct)
    {
        var (_, _, refreshCookie, refreshExpiry) = await _tokenStore.GetDecryptedTokensAsync(chatId, ct);
        if (refreshCookie is null || refreshExpiry is null || refreshExpiry <= DateTimeOffset.UtcNow)
            return ApiResult<TokenPairResult>.Failure(new ApiError(ApiErrorKind.Unauthorized, 401, "Сесія прострочена, потрібен повторний вхід."));

        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        msg.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");

        using var response = await _http.SendAsync(msg, ct);
        return await HandleAuthResponseAsync(chatId, response, ct);
    }

    private async Task<ApiResult<TokenPairResult>> HandleAuthResponseAsync(long chatId, HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            return ApiResult<TokenPairResult>.Failure(await BuildErrorAsync(response, ct));

        var body = await response.Content.ReadFromJsonAsync<AccessTokenBody>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Auth endpoint returned an empty body.");

        var cookieValue = ExtractSetCookieValue(response, RefreshCookieName)
                           ?? throw new InvalidOperationException(
                               $"Response to {response.RequestMessage?.RequestUri} did not include a '{RefreshCookieName}' Set-Cookie header.");

        // The API sets a 7-day expiry on the cookie (see AuthController.SetRefreshTokenCookie).
        // We mirror that here rather than parsing Set-Cookie's own Expires attribute, since the
        // bot only needs "roughly when to stop trying silently" — a slightly stale value just
        // means one extra failed refresh attempt before we ask the user to log in again.
        var refreshExpiry = DateTimeOffset.UtcNow.AddDays(7);

        // Read straight from the token's own "exp" claim instead of hardcoding/guessing the
        // backend's configured Jwt:ExpiryMinutes — stays correct even if that config changes.
        var accessExpiry = TryReadJwtExpiry(body.AccessToken) ?? DateTimeOffset.UtcNow.AddMinutes(10);

        await _tokenStorePublic.SaveTokenPairAsync(chatId, body.AccessToken, accessExpiry, cookieValue, refreshExpiry, ct);

        return ApiResult<TokenPairResult>.Success(new TokenPairResult(body.AccessToken, cookieValue, refreshExpiry));
    }

    private static string? ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            return null;

        foreach (var header in setCookieHeaders)
        {
            var firstSegment = header.Split(';', 2)[0];
            var parts = firstSegment.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim().Equals(cookieName, StringComparison.OrdinalIgnoreCase))
                return parts[1].Trim();
        }

        return null;
    }

    private sealed record AccessTokenBody(string AccessToken);

    /// <summary>Decodes the unvalidated "exp" claim from a JWT — we don't need to verify the
    /// signature here, only to know when *we* should stop presenting this token, since the
    /// backend itself is the actual authority that rejects an expired one.</summary>
    private static DateTimeOffset? TryReadJwtExpiry(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;

            var payloadJson = System.Text.Json.JsonDocument.Parse(Base64UrlDecode(parts[1]));
            if (!payloadJson.RootElement.TryGetProperty("exp", out var expElement)) return null;

            return DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64());
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    // ---------------------------------------------------------------- QuizSets

    public Task<ApiResult<CreatedQuizSetResponse>> CreateQuizSetAsync(long chatId, CreateQuizSetRequest request, CancellationToken ct) =>
        SendAuthorizedAsync<CreatedQuizSetResponse>(chatId, HttpMethod.Post, "/api/quizsets", request, ct);

    public Task<ApiResult<QuizSetDetailsResponse>> GetQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct) =>
        SendAuthorizedAsync<QuizSetDetailsResponse>(chatId, HttpMethod.Get, $"/api/quizsets/{quizSetId}", null, ct);

    public Task<ApiResult<object?>> UpdateQuizSetAsync(long chatId, Guid quizSetId, UpdateQuizSetRequest request, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Put, $"/api/quizsets/{quizSetId}", request, ct);

    public Task<ApiResult<object?>> DeleteQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Delete, $"/api/quizsets/{quizSetId}", null, ct);

    public Task<ApiResult<IReadOnlyList<QuizSetSummaryResponse>>> GetMyQuizSetsAsync(long chatId, CancellationToken ct) =>
        SendAuthorizedAsync<IReadOnlyList<QuizSetSummaryResponse>>(chatId, HttpMethod.Get, "/api/quizsets/my", null, ct);

    public Task<ApiResult<PagedResponse<QuizSetSummaryResponse>>> GetPublicQuizSetsAsync(long chatId, int pageNumber, int pageSize, CancellationToken ct) =>
        SendAuthorizedAsync<PagedResponse<QuizSetSummaryResponse>>(
            chatId, HttpMethod.Get, $"/api/quizsets/public?pageNumber={pageNumber}&pageSize={pageSize}", null, ct, requiresAuth: false);

    public Task<ApiResult<object?>> PublishQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Post, $"/api/quizsets/{quizSetId}/publish", null, ct);

    public Task<ApiResult<object?>> UnpublishQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Post, $"/api/quizsets/{quizSetId}/unpublish", null, ct);

    // ---------------------------------------------------------------- Questions

    public Task<ApiResult<object?>> AddQuestionAsync(long chatId, Guid quizSetId, AddQuestionRequest request, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Post, $"/api/quizsets/{quizSetId}/questions", request, ct);

    public Task<ApiResult<IReadOnlyList<QuestionResponse>>> GetQuestionsAsync(long chatId, Guid quizSetId, CancellationToken ct) =>
        SendAuthorizedAsync<IReadOnlyList<QuestionResponse>>(chatId, HttpMethod.Get, $"/api/quizsets/{quizSetId}/questions", null, ct);

    public Task<ApiResult<object?>> DeleteQuestionAsync(long chatId, Guid quizSetId, Guid questionId, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Delete, $"/api/quizsets/{quizSetId}/questions/{questionId}", null, ct);

    // ---------------------------------------------------------------- Game rooms

    public Task<ApiResult<CreateGameRoomResponse>> CreateGameRoomAsync(long chatId, CreateGameRoomRequest request, CancellationToken ct) =>
        SendAuthorizedAsync<CreateGameRoomResponse>(chatId, HttpMethod.Post, "/api/gamerooms", request, ct);

    public Task<ApiResult<JoinGameRoomResponse>> JoinGameRoomAsync(long chatId, string roomCode, JoinGameRoomRequest request, CancellationToken ct) =>
        // Joining works with or without a token (guest vs registered) — never force a refresh loop for guests.
        SendAuthorizedAsync<JoinGameRoomResponse>(chatId, HttpMethod.Post, $"/api/gamerooms/{roomCode}/join", request, ct, requiresAuth: false);

    public Task<ApiResult<object?>> StartGameAsync(long chatId, string roomCode, CancellationToken ct) =>
        SendAuthorizedNoContentAsync(chatId, HttpMethod.Post, $"/api/gamerooms/{roomCode}/start", null, ct);

    // ---------------------------------------------------------------- Core send pipeline

    private async Task<ApiResult<T>> SendAuthorizedAsync<T>(
        long chatId, HttpMethod method, string url, object? body, CancellationToken ct, bool requiresAuth = true)
    {
        var attempt = await SendOnceAsync(chatId, method, url, body, requiresAuth, ct);

        if (attempt.StatusCode == HttpStatusCode.Unauthorized && requiresAuth)
        {
            // Access token expired mid-flight: refresh once and retry exactly once.
            // A second 401 after a successful refresh means the account itself is the
            // problem (e.g. banned), not the token — surfaced to the caller as Unauthorized.
            var refreshed = await RefreshAsync(chatId, ct);
            if (refreshed.IsSuccess)
                attempt = await SendOnceAsync(chatId, method, url, body, requiresAuth, ct);
        }

        if (attempt.StatusCode == HttpStatusCode.Unauthorized || attempt.StatusCode == HttpStatusCode.Forbidden)
            await _tokenStorePublic.RecordAuthFailureAsync(chatId, ct);
        else if (attempt.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
            await _tokenStorePublic.RecordAuthSuccessAsync(chatId, ct);

        using var response = attempt.Response;
        if (!response.IsSuccessStatusCode)
            return ApiResult<T>.Failure(await BuildErrorAsync(response, ct));

        if (response.StatusCode == HttpStatusCode.NoContent)
            return ApiResult<T>.Success(default!);

        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        return ApiResult<T>.Success(value!);
    }

    private async Task<ApiResult<object?>> SendAuthorizedNoContentAsync(
        long chatId, HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var result = await SendAuthorizedAsync<object?>(chatId, method, url, body, ct);
        return result;
    }

    private async Task<(HttpStatusCode StatusCode, HttpResponseMessage Response)> SendOnceAsync(
        long chatId, HttpMethod method, string url, object? body, bool requiresAuth, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        if (requiresAuth)
        {
            var (accessToken, accessExpiry, _, _) = await _tokenStore.GetDecryptedTokensAsync(chatId, ct);

            if (accessToken is not null && accessExpiry is not null && accessExpiry > DateTimeOffset.UtcNow.AddSeconds(10))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            else if (accessToken is not null)
            {
                // Token present but expired/near-expiry: proactively refresh instead of
                // waiting for a guaranteed 401, saving one round trip.
                var refreshed = await RefreshAsync(chatId, ct);
                if (refreshed.IsSuccess)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Value!.AccessToken);
            }
        }

        // Retry/circuit-breaker policies are attached at the HttpClient registration level
        // (see DependencyInjection.cs) via Microsoft.Extensions.Http.Polly, so a plain
        // SendAsync here already benefits from them.
        var response = await _http.SendAsync(request, ct);
        return (response.StatusCode, response);
    }

    private static async Task<ApiError> BuildErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new ApiError(ApiErrorKind.Unauthorized, 401, "Потрібна повторна автентифікація.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ApiError(ApiErrorKind.Forbidden, 403, "Доступ заборонено.");

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken: ct);
            if (problem is not null)
                return new ApiError(ApiErrorKind.Problem, (int)response.StatusCode, problem.Title ?? "Помилка запиту.", problem.Errors);
        }
        catch
        {
            // Body wasn't ProblemDetails JSON (e.g. a raw 5xx from a proxy) — fall through.
        }

        return new ApiError(ApiErrorKind.Transport, (int)response.StatusCode, "Сервіс тимчасово недоступний.");
    }

    private sealed record ProblemDetailsBody(string? Type, string? Title, int? Status, Dictionary<string, string[]>? Errors);
}
