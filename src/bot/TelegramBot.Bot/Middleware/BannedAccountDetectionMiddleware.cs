using Telegram.Bot;
using TelegramBot.Application.Interfaces;

namespace TelegramBot.Bot.Middleware;

/// <summary>
/// QuizArenaApiClient flags a session as "possibly banned" after repeated 401/403
/// responses (see EfTokenStore.RecordAuthFailureAsync). This middleware is the single
/// place that turns that flag into a human message — individual handlers never inspect
/// raw ProblemDetails/status codes for this case, so the message stays consistent
/// everywhere a banned user might poke the bot.
/// </summary>
public sealed class BannedAccountDetectionMiddleware(ITokenStore tokenStore, ITelegramBotClient botClient) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct)
    {
        var session = await tokenStore.GetAsync(context.ChatId, ct);
        if (session is { IsFlaggedAsPossiblyBanned: true })
        {
            context.ShortCircuited = true;
            await botClient.SendMessage(
                context.ChatId,
                "Схоже, твій акаунт заблокований адміністрацією QuizArena. Якщо вважаєш, що це помилка, зверніться до підтримки платформи.",
                cancellationToken: ct);
            return;
        }

        await next();
    }
}
