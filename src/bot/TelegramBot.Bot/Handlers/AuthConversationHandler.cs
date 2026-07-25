using Telegram.Bot;
using TelegramBot.Application.Common;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

/// <summary>Drives both Registering and LoggingIn flows — they share the same step
/// shape (LoggingIn just skips NickName), so one handler avoids duplicating the
/// "which step am I on" branching twice.</summary>
public sealed class AuthConversationHandler(
    ITelegramBotClient botClient,
    IConversationStateStore stateStore,
    IQuizArenaApiClient apiClient)
{
    public async Task StartRegistrationAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.SetAsync(context.ChatId,
            new ConversationContext { Flow = ConversationFlow.Registering, Step = ConversationStep.NickName }, ct);

        await botClient.SendMessage(context.ChatId, "Як тебе називати в квізах? Введи нік (2–20 символів).",
            replyMarkup: KeyboardFactory.Remove(), cancellationToken: ct);
    }

    public async Task StartLoginAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.SetAsync(context.ChatId,
            new ConversationContext { Flow = ConversationFlow.LoggingIn, Step = ConversationStep.Email }, ct);

        await botClient.SendMessage(context.ChatId, "Введи email, яким реєструвався(-лась) в QuizArena.",
            replyMarkup: KeyboardFactory.Remove(), cancellationToken: ct);
    }

    public async Task HandleMessageAsync(UpdateContext context, ConversationContext convo, string text, CancellationToken ct)
    {
        switch (convo.Step)
        {
            case ConversationStep.NickName:
                if (text.Length is < 2 or > 20)
                {
                    await botClient.SendMessage(context.ChatId, "Нік має бути від 2 до 20 символів. Спробуй ще раз.", cancellationToken: ct);
                    return;
                }
                convo.Data.NickName = text;
                convo.Step = ConversationStep.Email;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Тепер введи email.", cancellationToken: ct);
                return;

            case ConversationStep.Email:
                if (!text.Contains('@'))
                {
                    await botClient.SendMessage(context.ChatId, "Схоже, це не email. Спробуй ще раз.", cancellationToken: ct);
                    return;
                }
                convo.Data.Email = text;
                convo.Step = ConversationStep.Password;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId,
                    convo.Flow == ConversationFlow.Registering
                        ? "Придумай пароль (мінімум 8 символів)."
                        : "Введи пароль.",
                    cancellationToken: ct);
                return;

            case ConversationStep.Password:
                await CompleteAsync(context, convo, password: text, ct);
                return;
        }
    }

    private async Task CompleteAsync(UpdateContext context, ConversationContext convo, string password, CancellationToken ct)
    {
        ApiResult<TokenPairResult> result = convo.Flow == ConversationFlow.Registering
            ? await apiClient.RegisterAsync(context.ChatId, new RegisterRequest(convo.Data.NickName!, convo.Data.Email!, password), ct)
            : await apiClient.LoginAsync(context.ChatId, new LoginRequest(convo.Data.Email!, password), ct);

        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, DescribeAuthError(result.Error!, convo.Flow), cancellationToken: ct);
            // Stay on the Password step so the user can just retype the password
            // (registering: they might also need a new email, so bump back a step there).
            if (convo.Flow == ConversationFlow.Registering && result.Error!.Kind == ApiErrorKind.Problem &&
                result.Error.ValidationErrors?.ContainsKey("Email") == true)
            {
                convo.Step = ConversationStep.Email;
                await stateStore.SetAsync(context.ChatId, convo, ct);
                await botClient.SendMessage(context.ChatId, "Введи інший email.", cancellationToken: ct);
            }
            return;
        }

        await stateStore.ClearAsync(context.ChatId, ct);
        await botClient.SendMessage(context.ChatId, $"Готово! Ласкаво просимо, {convo.Data.NickName ?? ""}. 🎉",
            replyMarkup: KeyboardFactory.MainMenu(isAuthenticated: true), cancellationToken: ct);
    }

    private static string DescribeAuthError(ApiError error, ConversationFlow flow) => error switch
    {
        { Kind: ApiErrorKind.Problem, ValidationErrors: { } errors } when errors.ContainsKey("Email") =>
            flow == ConversationFlow.Registering
                ? "Цей email вже зареєстрований. Спробуй увійти або вкажи інший email."
                : "Не знайшли акаунт з таким email.",
        { Kind: ApiErrorKind.Problem, ValidationErrors: { } errors } when errors.ContainsKey("Password") =>
            "Пароль не відповідає вимогам (мінімум 8 символів, є і літери, і цифри).",
        { Kind: ApiErrorKind.Unauthorized } => "Невірний email або пароль.",
        { Kind: ApiErrorKind.Transport } => "QuizArena тимчасово недоступна. Спробуй ще раз за хвилину.",
        _ => "Щось пішло не так. Спробуй ще раз."
    };
}
