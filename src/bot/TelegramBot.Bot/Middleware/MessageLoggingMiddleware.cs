using Telegram.Bot.Types.Enums;
using TelegramBot.Application.Interfaces;
using TelegramBot.Domain.Entities;
using TelegramBot.Infrastructure.Persistence;

namespace TelegramBot.Bot.Middleware;

public sealed class MessageLoggingMiddleware(BotDbContext dbContext, ITokenStore tokenStore) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct)
    {
        if (context.Update.Type == UpdateType.Message && context.Update.Message?.Text is { } text)
        {
            var session = await tokenStore.GetAsync(context.ChatId, ct);

            dbContext.MessageLogs.Add(new BotMessageLog
            {
                ChatId = context.ChatId,
                TelegramUserId = context.TelegramUserId,
                TelegramUsername = context.TelegramUsername,
                NickName = session?.NickName,
                Text = text.Length > 4096 ? text[..4096] : text,
                SentAtUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(ct);
        }

        await next();
    }
}
