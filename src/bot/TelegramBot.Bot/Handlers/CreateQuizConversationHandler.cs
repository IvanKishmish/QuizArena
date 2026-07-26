using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

public sealed class CreateQuizConversationHandler(
    ITelegramBotClient botClient,
    IConversationStateStore stateStore,
    IQuizArenaApiClient apiClient)
{
    private const int MinOptionsBeforeFinish = 2;

    public async Task StartAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.SetAsync(context.ChatId,
            new ConversationContext { Flow = ConversationFlow.CreatingQuiz, Step = ConversationStep.QuizTitle }, ct);
        await botClient.SendMessage(context.ChatId, "Назва квізу?", replyMarkup: KeyboardFactory.Remove(), cancellationToken: ct);
    }

    public async Task StartEditAsync(UpdateContext context, Guid quizSetId, CancellationToken ct)
    {
        var convo = new ConversationContext { Flow = ConversationFlow.EditingQuiz, Step = ConversationStep.EditQuizTitle };
        convo.Data.QuizSetId = quizSetId;
        await stateStore.SetAsync(context.ChatId, convo, ct);
        await botClient.SendMessage(context.ChatId, "Нова назва квізу?", replyMarkup: KeyboardFactory.Remove(), cancellationToken: ct);
    }

    public async Task HandleMessageAsync(UpdateContext context, ConversationContext convo, string text, CancellationToken ct)
    {
        switch (convo.Step)
        {
            case ConversationStep.QuizTitle:
                convo.Data.QuizTitle = text;
                convo.Step = ConversationStep.QuizDescription;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Короткий опис квізу?", cancellationToken: ct);
                return;

            case ConversationStep.QuizDescription:
                convo.Data.QuizDescription = text;
                await CreateQuizAndAskFirstQuestionAsync(context, convo, ct);
                return;

            case ConversationStep.EditQuizTitle:
                convo.Data.QuizTitle = text;
                convo.Step = ConversationStep.EditQuizDescription;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Новий опис квізу?", cancellationToken: ct);
                return;

            case ConversationStep.EditQuizDescription:
                convo.Data.QuizDescription = text;
                await SaveEditAsync(context, convo, ct);
                return;

            case ConversationStep.QuestionText:
                convo.Data.QuestionText = text;
                convo.Step = ConversationStep.QuestionType;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Тип питання:", replyMarkup: KeyboardFactory.QuestionTypeChoice(), cancellationToken: ct);
                return;

            case ConversationStep.QuestionTimeLimit:
                if (!int.TryParse(text, out var seconds) || seconds is < 5 or > 120)
                {
                    await botClient.SendMessage(context.ChatId, "Введи число секунд від 5 до 120.", cancellationToken: ct);
                    return;
                }
                convo.Data.QuestionTimeLimitSeconds = seconds;
                convo.Step = ConversationStep.QuestionPoints;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Скільки балів за правильну відповідь?", cancellationToken: ct);
                return;

            case ConversationStep.QuestionPoints:
                if (!int.TryParse(text, out var points) || points is < 1 or > 1000)
                {
                    await botClient.SendMessage(context.ChatId, "Введи число балів від 1 до 1000.", cancellationToken: ct);
                    return;
                }
                convo.Data.QuestionPoints = points;
                convo.Data.PendingOptions.Clear();
                convo.Step = ConversationStep.QuestionOptionText;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Текст варіанту відповіді №1:", cancellationToken: ct);
                return;

            case ConversationStep.QuestionOptionText:
                // Scratch slot: QuizDescription is no longer needed once the quiz exists,
                // so we reuse it to hold "text of the option currently being entered"
                // rather than adding a one-off field to ConversationData for a single step.
                convo.Data.QuizDescription = text;
                convo.Step = ConversationStep.QuestionOptionIsCorrect;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Це правильна відповідь?", replyMarkup: KeyboardFactory.YesNo("opt:correct", "opt:incorrect"), cancellationToken: ct);
                return;
        }
    }

    public async Task HandleQuestionTypeChosenAsync(UpdateContext context, ConversationContext convo, QuestionType type, CancellationToken ct)
    {
        convo.Data.QuestionTypeRaw = (int)type;
        convo.Step = ConversationStep.QuestionTimeLimit;
        await stateStore.SetAsync(context.ChatId, convo, ct);
        await botClient.SendMessage(context.ChatId, "Ліміт часу на відповідь, у секундах (5–120)?", cancellationToken: ct);
    }

    public async Task HandleOptionCorrectnessChosenAsync(UpdateContext context, ConversationContext convo, bool isCorrect, CancellationToken ct)
    {
        var optionText = convo.Data.QuizDescription ?? string.Empty; // scratch slot, see above
        convo.Data.PendingOptions.Add(new PendingOption(optionText, isCorrect, convo.Data.PendingOptions.Count));

        var count = convo.Data.PendingOptions.Count;
        if (count >= MinOptionsBeforeFinish)
        {
            convo.Step = ConversationStep.QuestionOptionText;
            await stateStore.SetAsync(context.ChatId, convo, ct);
            await botClient.SendMessage(context.ChatId, "Варіант додано. Додати ще один, чи достатньо?",
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"➕ Додати варіант №{count + 1}", "opt:more"),
                    InlineKeyboardButton.WithCallbackData("✅ Достатньо, зберегти питання", "opt:finish"),
                }), cancellationToken: ct);
        }
        else
        {
            convo.Step = ConversationStep.QuestionOptionText;
            await stateStore.SetAsync(context.ChatId, convo, ct);
            await botClient.SendMessage(context.ChatId, $"Текст варіанту відповіді №{count + 1}:", cancellationToken: ct);
        }
    }

    public async Task HandleAddAnotherOptionAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        convo.Step = ConversationStep.QuestionOptionText;
        await stateStore.SetAsync(context.ChatId, convo, ct);
        await botClient.SendMessage(context.ChatId, $"Текст варіанту відповіді №{convo.Data.PendingOptions.Count + 1}:", cancellationToken: ct);
    }

    public async Task HandleFinishQuestionAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        var request = new AddQuestionRequest(
            convo.Data.QuestionText!,
            (QuestionType)convo.Data.QuestionTypeRaw!.Value,
            convo.Data.QuestionTimeLimitSeconds!.Value,
            convo.Data.QuestionPoints!.Value,
            convo.Data.PendingOptions.Select(o => new AnswerOptionRequest(o.Text, o.IsCorrect, o.OrderIndex)).ToList());

        var result = await apiClient.AddQuestionAsync(context.ChatId, convo.Data.QuizSetId!.Value, request, ct);

        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось зберегти питання. Перевір, чи є хоча б одна правильна відповідь.", cancellationToken: ct);
            return;
        }

        convo.Data.QuestionText = null;
        convo.Data.QuestionTypeRaw = null;
        convo.Data.QuestionTimeLimitSeconds = null;
        convo.Data.QuestionPoints = null;
        convo.Data.PendingOptions.Clear();
        convo.Step = ConversationStep.QuestionText;
        await stateStore.SetAsync(context.ChatId, convo, ct);

        await botClient.SendMessage(context.ChatId, "Питання збережено ✅ Наступне питання, чи завершуємо?",
            replyMarkup: new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Наступне питання", "quiz:nextquestion"),
                InlineKeyboardButton.WithCallbackData("🏁 Завершити додавання питань", "quiz:finish"),
            }), cancellationToken: ct);
    }

    public async Task StartNextQuestionAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        convo.Step = ConversationStep.QuestionText;
        await stateStore.SetAsync(context.ChatId, convo, ct);
        await botClient.SendMessage(context.ChatId, "Текст питання:", cancellationToken: ct);
    }

    public async Task FinishAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        await stateStore.ClearAsync(context.ChatId, ct);
        await botClient.SendMessage(context.ChatId, "Квіз готовий! Не забудь опублікувати його, щоб інші могли грати.",
            replyMarkup: KeyboardFactory.QuizDetailsActions(convo.Data.QuizSetId!.Value, isPublished: false), cancellationToken: ct);
    }

    private async Task CreateQuizAndAskFirstQuestionAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        var result = await apiClient.CreateQuizSetAsync(context.ChatId,
            new CreateQuizSetRequest(convo.Data.QuizTitle!, convo.Data.QuizDescription!), ct);

        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось створити квіз. Спробуй ще раз /newquiz.", cancellationToken: ct);
            await stateStore.ClearAsync(context.ChatId, ct);
            return;
        }

        convo.Data.QuizSetId = result.Value!.Id;
        convo.Step = ConversationStep.QuestionText;
        await stateStore.SetAsync(context.ChatId, convo, ct);

        await botClient.SendMessage(context.ChatId, "Квіз створено. Тепер додамо питання.\n\nТекст першого питання:", cancellationToken: ct);
    }

    private async Task SaveEditAsync(UpdateContext context, ConversationContext convo, CancellationToken ct)
    {
        var result = await apiClient.UpdateQuizSetAsync(context.ChatId, convo.Data.QuizSetId!.Value,
            new UpdateQuizSetRequest(convo.Data.QuizTitle!, convo.Data.QuizDescription!), ct);

        await stateStore.ClearAsync(context.ChatId, ct);

        await botClient.SendMessage(context.ChatId,
            result.IsSuccess ? "Квіз оновлено ✅" : "Не вдалось оновити квіз. Спробуй ще раз.",
            cancellationToken: ct);
    }
}
