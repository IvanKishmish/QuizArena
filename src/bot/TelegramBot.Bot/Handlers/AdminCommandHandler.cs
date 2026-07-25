using Telegram.Bot;
using TelegramBot.Application.Common;
using TelegramBot.Application.Contracts.Api;
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
    IConversationStateStore stateStore,
    IQuizArenaApiClient apiClient,
    MenuMessenger menu)
{
    private const int PageSize = 10;

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

    public async Task ShowDashboardAsync(UpdateContext context, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.GetDashboardAsync(context.ChatId, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);
            return;
        }

        var stats = result.Value!;
        await botClient.SendMessage(context.ChatId,
            "📊 *QuizArena — панель адміністратора*\n" +
            $"Користувачів: {stats.TotalUsers}\n" +
            $"Квізів усього: {stats.TotalQuizSets}\n" +
            $"Опубліковано: {stats.TotalPublishedQuizSets}\n" +
            $"Зіграно ігор: {stats.TotalGamesPlayed}",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: ct);
    }

    public async Task ShowUsersAsync(UpdateContext context, int page, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.GetUsersAsync(context.ChatId, page, PageSize, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);
            return;
        }

        var paged = result.Value!;
        if (paged.Items.Count == 0)
        {
            await botClient.SendMessage(context.ChatId, "Користувачів не знайдено.", cancellationToken: ct);
            return;
        }

        var lines = paged.Items.Select(u =>
            $"{(u.IsBanned ? "🚫" : "✅")} *{u.Nickname}* — {u.Email}\nID: `{u.Id}`, з {u.RegisteredAt:yyyy-MM-dd}");

        await menu.ShowAsync(context.ChatId,
            $"👥 *Користувачі* (стор. {paged.PageNumber}/{Math.Max(paged.TotalPages, 1)})\n\n" + string.Join("\n\n", lines),
            KeyboardFactory.AdminUsersList(paged.Items, paged.PageNumber, Math.Max(paged.TotalPages, 1)),
            ct, Telegram.Bot.Types.Enums.ParseMode.Markdown);
    }

    public async Task BanUserAsync(UpdateContext context, Guid userId, int page, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.BanUserAsync(context.ChatId, userId, ct);
        if (!result.IsSuccess)
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);

        await ShowUsersAsync(context, page, ct);
    }

    public async Task UnbanUserAsync(UpdateContext context, Guid userId, int page, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.UnbanUserAsync(context.ChatId, userId, ct);
        if (!result.IsSuccess)
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);

        await ShowUsersAsync(context, page, ct);
    }

    public async Task ShowQuizSetsAsync(UpdateContext context, int page, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.GetQuizSetsForModerationAsync(context.ChatId, page, PageSize, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);
            return;
        }

        var paged = result.Value!;
        if (paged.Items.Count == 0)
        {
            await botClient.SendMessage(context.ChatId, "Квізів не знайдено.", cancellationToken: ct);
            return;
        }

        var lines = paged.Items.Select(q =>
            $"{(q.Visibility == QuizVisibility.Public ? "🟢" : "⚪")} *{q.Title}*\nВласник: `{q.OwnerId}`, створено {q.CreatedAt:yyyy-MM-dd}");

        await menu.ShowAsync(context.ChatId,
            $"🗂 *Модерація квізів* (стор. {paged.PageNumber}/{Math.Max(paged.TotalPages, 1)})\n\n" + string.Join("\n\n", lines),
            KeyboardFactory.AdminQuizSetsList(paged.Items, paged.PageNumber, Math.Max(paged.TotalPages, 1)),
            ct, Telegram.Bot.Types.Enums.ParseMode.Markdown);
    }

    public async Task DeleteQuizSetAsync(UpdateContext context, Guid quizSetId, int page, CancellationToken ct)
    {
        if (!context.IsAdmin) return;

        var result = await apiClient.DeleteAnyQuizSetAsync(context.ChatId, quizSetId, ct);
        if (!result.IsSuccess)
            await botClient.SendMessage(context.ChatId, DescribeAdminError(result.Error!), cancellationToken: ct);

        await ShowQuizSetsAsync(context, page, ct);
    }

    private static string DescribeAdminError(ApiError error) => error.Kind switch
    {
        ApiErrorKind.Forbidden =>
            "QuizArena відхилила запит (403). Ти в списку адмінів бота, але акаунт, під яким ти залогінений у боті, " +
            "не має ролі Admin на бекенді — зайди під акаунтом, якому цю роль видано.",
        ApiErrorKind.Unauthorized => "Спочатку залогінься в QuizArena (🔑 Увійти), потім повтори команду.",
        ApiErrorKind.Transport => "QuizArena тимчасово недоступна. Спробуй ще раз за хвилину.",
        _ => "Не вдалось виконати дію."
    };
}
