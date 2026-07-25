using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;
using TelegramBot.Bot.Options;

namespace TelegramBot.Bot.Middleware;

/// <summary>
/// Fixed 60s window counter per chat, kept in Redis (not in-process) so the limit holds
/// even if the bot is later scaled to multiple instances behind one webhook. Runs first
/// in the pipeline — a spamming chat shouldn't even cost us an auth/DB lookup.
/// </summary>
public sealed class RateLimitingMiddleware(
    IConnectionMultiplexer redis,
    IOptions<RateLimitingOptions> options,
    ITelegramBotClient botClient) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct)
    {
        var key = $"bot:ratelimit:{context.ChatId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var db = redis.GetDatabase();
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
            await db.KeyExpireAsync(key, TimeSpan.FromSeconds(65));

        if (count > options.Value.MaxCommandsPerMinutePerChat)
        {
            context.ShortCircuited = true;
            if (count == options.Value.MaxCommandsPerMinutePerChat + 1) // warn once per window, not on every extra message
                await botClient.SendMessage(context.ChatId, "Забагато повідомлень поспіль. Зроби паузу і спробуй за хвилину.", cancellationToken: ct);
            return;
        }

        await next();
    }
}
