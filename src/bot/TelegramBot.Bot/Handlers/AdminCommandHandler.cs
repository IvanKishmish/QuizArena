using Telegram.Bot;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

public sealed class AdminCommandHandler(
    ITelegramBotClient botClient,
    IBotStatsService statsService,
    IMaintenanceModeService maintenanceMode,
    IBroadcastService broadcastService,
    IConversationStateStore stateStore)
{
    public async Task ShowStatsAsync(UpdateContext context, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var snapshot = await statsService.GetSnapshotAsync(ct);
        await botClient.SendMessage(context.ChatId,
            "📊 *Статистика бота*\n" +
            $"Унікальних користувачів: {snapshot.TotalUniqueUsersEver}\n" +
            $"Активних ігрових сесій зараз: {snapshot.ActiveGameSessionsNow}\n" +
            $"Повідомлень оброблено сьогодні: {snapshot.MessagesProcessedToday}",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: ct);
    }

    public async Task StartBroadcastAsync(UpdateContext context, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        await stateStore.SetAsync(context.ChatId,
            new ConversationContext { Flow = ConversationFlow.AwaitingBroadcastMessage, Step = ConversationStep.BroadcastMessage }, ct);
        await botClient.SendMessage(context.ChatId, "Введи текст розсилки для всіх користувачів бота:", cancellationToken: ct);
    }

    public async Task HandleBroadcastMessageEnteredAsync(UpdateContext context, ConversationContext convo, string text, CancellationToken ct)
    {
        convo.Data.BroadcastMessage = text;
        convo.Step = ConversationStep.BroadcastConfirmation;
        await stateStore.SetAsync(context.ChatId, convo, ct);

        var count = await broadcastService.GetRecipientCountAsync(ct);
        await botClient.SendMessage(context.ChatId,
            $"Розіслати це повідомлення {count} користувачам?\n\n—\n{text}\n—",
            replyMarkup: KeyboardFactory.YesNo("broadcast:confirm", "broadcast:cancel"), cancellationToken: ct);
    }

    public async Task ConfirmBroadcastAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        var text = convo.Data.BroadcastMessage!;
        await stateStore.ClearAsync(context.ChatId, ct);

        await botClient.SendMessage(context.ChatId, "Розсилаю…", cancellationToken: ct);
        var (success, failure) = await broadcastService.BroadcastAsync(context.TelegramUserId, text, ct);
        await botClient.SendMessage(context.ChatId, $"Готово. Доставлено: {success}, не вдалось: {failure}.", cancellationToken: ct);
    }

    public async Task CancelBroadcastAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.ClearAsync(context.ChatId, ct);
        await botClient.SendMessage(context.ChatId, "Розсилку скасовано.", cancellationToken: ct);
    }

    public async Task SetMaintenanceAsync(UpdateContext context, bool enabled, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        await maintenanceMode.SetAsync(enabled, ct);
        await botClient.SendMessage(context.ChatId, enabled ? "Режим обслуговування увімкнено." : "Режим обслуговування вимкнено.", cancellationToken: ct);
    }
}
