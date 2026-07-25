namespace TelegramBot.Application.Common;

/// <summary>
/// Outcome of a call to the QuizArena API. Wraps either a value or a normalized error,
/// so handlers never have to catch HttpRequestException / parse ProblemDetails themselves.
/// </summary>
public sealed class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public ApiError? Error { get; private init; }

    public static ApiResult<T> Success(T value) => new() { IsSuccess = true, Value = value };

    public static ApiResult<T> Failure(ApiError error) => new() { IsSuccess = false, Error = error };
}

public sealed record ApiError(
    ApiErrorKind Kind,
    int? StatusCode,
    string Title,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public enum ApiErrorKind
{
    /// <summary>4xx with a ProblemDetails body — e.g. validation, conflict, not found.</summary>
    Problem,

    /// <summary>401 — access token missing/expired and refresh also failed.</summary>
    Unauthorized,

    /// <summary>403 — authenticated but forbidden (e.g. banned by a backend admin).</summary>
    Forbidden,

    /// <summary>Network failure, timeout, or non-JSON 5xx after Polly retries were exhausted.</summary>
    Transport
}
