using Telegram.Bot;
using TelegramBot.Application.Interfaces;

namespace TelegramBot.Bot.Middleware;

public sealed class MaintenanceModeMiddleware(IMaintenanceModeService maintenanceMode, ITelegramBotClient botClient) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, Func<Task> next, CancellationToken ct)
    {
        if (context.IsAdmin || !await maintenanceMode.IsEnabledAsync(ct))
        {
            await next();
            return;
        }

        context.ShortCircuited = true;
        await botClient.SendMessage(context.ChatId, "Бот тимчасово недоступний, спробуйте пізніше.", cancellationToken: ct);
    }
}
