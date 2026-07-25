using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

public sealed class QuizMenuHandler(ITelegramBotClient botClient, IQuizArenaApiClient apiClient, MenuMessenger menu)
{
    private const int PublicPageSize = 5;

    public async Task ShowMyQuizzesAsync(UpdateContext context, CancellationToken ct)
    {
        var result = await apiClient.GetMyQuizSetsAsync(context.ChatId, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось отримати список квізів. Спробуй ще раз.", cancellationToken: ct);
            return;
        }

        var quizzes = result.Value!;
        if (quizzes.Count == 0)
        {
            await menu.ShowAsync(context.ChatId, "У тебе ще немає квізів.", KeyboardFactory.MyQuizzesList([]), ct);
            return;
        }

        var items = quizzes.Select(q => (q.Id, q.Title, q.IsPublished)).ToList();
        await menu.ShowAsync(context.ChatId, "Твої квізи:", KeyboardFactory.MyQuizzesList(items), ct);
    }

    public async Task ShowPublicCatalogAsync(UpdateContext context, int page, CancellationToken ct)
    {
        var result = await apiClient.GetPublicQuizSetsAsync(context.ChatId, page, PublicPageSize, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Каталог тимчасово недоступний.", cancellationToken: ct);
            return;
        }

        var paged = result.Value!;
        if (paged.Items.Count == 0)
        {
            await botClient.SendMessage(context.ChatId, "Публічних квізів поки немає.", cancellationToken: ct);
            return;
        }

        var items = paged.Items.Select(q => (q.Id, q.Title)).ToList();
        await menu.ShowAsync(context.ChatId,
            $"Публічний каталог (стор. {paged.PageNumber}/{Math.Max(paged.TotalPages, 1)}):",
            KeyboardFactory.PublicCatalog(items, paged.PageNumber, Math.Max(paged.TotalPages, 1)), ct);
    }

    public async Task ShowQuizDetailsAsync(UpdateContext context, Guid quizId, CancellationToken ct)
    {
        var result = await apiClient.GetQuizSetAsync(context.ChatId, quizId, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось відкрити квіз.", cancellationToken: ct);
            return;
        }

        var quiz = result.Value!;
        var status = quiz.IsPublished ? "🟢 опубліковано" : "⚪ чернетка";
        await menu.ShowAsync(context.ChatId,
            $"*{Escape(quiz.Title)}*\n{Escape(quiz.Description)}\n\nСтатус: {status}",
            KeyboardFactory.QuizDetailsActions(quiz.Id, quiz.IsPublished), ct, ParseMode.MarkdownV2);
    }

    public async Task ShowQuestionsAsync(UpdateContext context, Guid quizId, CancellationToken ct)
    {
        var result = await apiClient.GetQuestionsAsync(context.ChatId, quizId, ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось отримати питання цього квізу.", cancellationToken: ct);
            return;
        }

        var questions = result.Value!;
        if (questions.Count == 0)
        {
            await menu.ShowAsync(context.ChatId, "У цього квізу поки немає питань.", KeyboardFactory.QuestionsList(quizId, []), ct);
            return;
        }

        var items = questions.Select(q => (q.Id, q.Text)).ToList();
        await menu.ShowAsync(context.ChatId, $"Питання квізу ({questions.Count}):", KeyboardFactory.QuestionsList(quizId, items), ct);
    }

    public async Task DeleteQuestionAsync(UpdateContext context, Guid quizId, Guid questionId, CancellationToken ct)
    {
        var result = await apiClient.DeleteQuestionAsync(context.ChatId, quizId, questionId, ct);

        if (!result.IsSuccess)
            await botClient.SendMessage(context.ChatId, "Не вдалось видалити питання.", cancellationToken: ct);

        await ShowQuestionsAsync(context, quizId, ct);
    }

    public async Task PublishAsync(UpdateContext context, Guid quizId, bool publish, CancellationToken ct)
    {
        var result = publish
            ? await apiClient.PublishQuizSetAsync(context.ChatId, quizId, ct)
            : await apiClient.UnpublishQuizSetAsync(context.ChatId, quizId, ct);

        await botClient.SendMessage(context.ChatId,
            result.IsSuccess
                ? (publish ? "Квіз опубліковано ✅" : "Квіз знято з публікації.")
                : "Не вдалось змінити статус квізу.",
            cancellationToken: ct);

        if (result.IsSuccess)
            await ShowQuizDetailsAsync(context, quizId, ct);
    }

    public async Task ConfirmDeleteAsync(UpdateContext context, Guid quizId, CancellationToken ct)
    {
        await botClient.SendMessage(context.ChatId, "Видалити цей квіз назавжди?",
            replyMarkup: KeyboardFactory.YesNo($"quiz:delete_confirm:{quizId}", "quiz:delete_cancel"), cancellationToken: ct);
    }

    public async Task DeleteAsync(UpdateContext context, Guid quizId, CancellationToken ct)
    {
        var result = await apiClient.DeleteQuizSetAsync(context.ChatId, quizId, ct);
        await botClient.SendMessage(context.ChatId, result.IsSuccess ? "Квіз видалено." : "Не вдалось видалити квіз.", cancellationToken: ct);
        await menu.ForgetAsync(context.ChatId, ct);
    }

    private static string Escape(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");
}
