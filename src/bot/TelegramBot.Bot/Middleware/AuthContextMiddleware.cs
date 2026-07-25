using TelegramBot.Application.Interfaces;

namespace TelegramBot.Bot.Middleware;

/// <summary>Populates UpdateContext.IsAuthenticated / IsAdmin so downstream handlers and the
/// maintenance-mode gate don't each re-query the token store / admin list themselves.</summary>
public sealed class AuthContextMiddleware(ITokenStore tokenStore, IBotAdminService botAdminService, IBotStatsService statsService) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct)
    {
        await tokenStore.EnsureSessionAsync(context.ChatId, context.TelegramUserId, context.TelegramUsername, ct);

        var session = await tokenStore.GetAsync(context.ChatId, ct);
        context.IsAuthenticated = session?.AccessTokenEncrypted is not null;
        context.IsAdmin = await botAdminService.IsAdminAsync(context.TelegramUserId, ct);

        await statsService.RecordMessageProcessedAsync(context.ChatId, ct);

        await next();
    }
}
