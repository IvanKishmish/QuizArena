using Telegram.Bot.Types;

namespace TelegramBot.Bot.Middleware;

public sealed class UpdateContext
{
    public required Update Update { get; init; }
    public required long ChatId { get; init; }
    public required long TelegramUserId { get; init; }
    public string? TelegramUsername { get; init; }
    public bool IsAdmin { get; set; }
    public bool IsAuthenticated { get; set; }

    /// <summary>Set to true by any middleware that fully handled the update (e.g. maintenance
    /// mode block), so downstream middleware/handlers are skipped.</summary>
    public bool ShortCircuited { get; set; }
}

/// <summary>
/// One link in the pipeline. Mirrors ASP.NET Core middleware's "call next() yourself"
/// shape so ordering and short-circuiting are explicit and testable, instead of one
/// large if/else switch handling cross-cutting concerns inline with business logic.
/// </summary>
public interface IUpdateMiddleware
{
    Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct);
}
