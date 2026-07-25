using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Handlers;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Routing;

/// <summary>
/// The terminal step of the middleware pipeline (see UpdatePipeline). Deliberately thin:
/// every branch is a one-line delegation to a handler — no business logic lives here.
/// </summary>
public sealed class UpdateRouter(
    ITelegramBotClient botClient,
    IConversationStateStore stateStore,
    StartCommandHandler startHandler,
    AuthConversationHandler authHandler,
    QuizMenuHandler quizMenuHandler,
    CreateQuizConversationHandler createQuizHandler,
    GameRoomHandler gameRoomHandler,
    GameLiveHandler gameLiveHandler,
    AdminCommandHandler adminHandler)
{
    public async Task RouteAsync(UpdateContext context, CancellationToken ct)
    {
        switch (context.Update.Type)
        {
            case UpdateType.Message when context.Update.Message?.Text is { } text:
                await RouteMessageAsync(context, text.Trim(), ct);
                return;

            case UpdateType.CallbackQuery when context.Update.CallbackQuery is { } query:
                await RouteCallbackAsync(context, query, ct);
                return;
        }
    }

    private async Task RouteMessageAsync(UpdateContext context, string text, CancellationToken ct)
    {
        // Commands always take precedence and implicitly abandon whatever FSM flow was active —
        // a user typing /start mid-registration means "start over", not "this is my nickname".
        if (text.StartsWith('/'))
        {
            await RouteCommandAsync(context, text, ct);
            return;
        }

        if (text is "🗂 Мої квізи")
        {
            await quizMenuHandler.ShowMyQuizzesAsync(context, ct);
            return;
        }

        if (text is "🌐 Каталог")
        {
            await quizMenuHandler.ShowPublicCatalogAsync(context, page: 1, ct);
            return;
        }

        if (text is "🔑 Увійти")
        {
            await authHandler.StartLoginAsync(context, ct);
            return;
        }

        if (text is "📝 Реєстрація")
        {
            await authHandler.StartRegistrationAsync(context, ct);
            return;
        }

        if (text is "🎮 Приєднатись до гри")
        {
            await gameRoomHandler.StartJoinFlowAsync(context, ct);
            return;
        }

        if (text is "🚪 Вийти")
        {
            await authHandler.LogoutAsync(context, ct);
            return;
        }

        var convo = await stateStore.GetAsync(context.ChatId, ct);
        await RouteConversationMessageAsync(context, convo, text, ct);
    }

    private async Task RouteConversationMessageAsync(UpdateContext context, ConversationContext convo, string text, CancellationToken ct)
    {
        switch (convo.Flow)
        {
            case ConversationFlow.Registering or ConversationFlow.LoggingIn:
                await authHandler.HandleMessageAsync(context, convo, text, ct);
                return;

            case ConversationFlow.CreatingQuiz or ConversationFlow.AddingQuestion:
                await createQuizHandler.HandleMessageAsync(context, convo, text, ct);
                return;

            case ConversationFlow.AwaitingRoomCodeToJoin:
                await gameRoomHandler.HandleRoomCodeEnteredAsync(context, convo, text, ct);
                return;

            case ConversationFlow.AwaitingGuestDisplayName:
                await gameRoomHandler.HandleGuestDisplayNameEnteredAsync(context, convo, text, ct);
                return;

            case ConversationFlow.AwaitingBroadcastMessage:
                await adminHandler.HandleBroadcastMessageEnteredAsync(context, convo, text, ct);
                return;

            default:
                await botClient.SendMessage(context.ChatId, "Не зрозумів. Скористайся меню, /start або /help.", cancellationToken: ct);
                return;
        }
    }

    private async Task RouteCommandAsync(UpdateContext context, string text, CancellationToken ct)
    {
        var command = text.Split(' ', '@')[0].ToLowerInvariant();

        switch (command)
        {
            case "/start":
                await startHandler.HandleAsync(context, ct);
                return;

            case "/help":
                await startHandler.HandleHelpAsync(context, ct);
                return;

            case "/newquiz":
                await createQuizHandler.StartAsync(context, ct);
                return;

            case "/join":
                await gameRoomHandler.StartJoinFlowAsync(context, ct);
                return;

            case "/logout":
                await authHandler.LogoutAsync(context, ct);
                return;

            case "/admin_stats":
                await adminHandler.ShowStatsAsync(context, ct);
                return;

            case "/admin_broadcast":
                await adminHandler.StartBroadcastAsync(context, ct);
                return;

            case "/admin_maintenance_on":
                await adminHandler.SetMaintenanceAsync(context, enabled: true, ct);
                return;

            case "/admin_maintenance_off":
                await adminHandler.SetMaintenanceAsync(context, enabled: false, ct);
                return;

            default:
                await botClient.SendMessage(context.ChatId, "Невідома команда. /help — щоб побачити список команд.", cancellationToken: ct);
                return;
        }
    }

    private async Task RouteCallbackAsync(UpdateContext context, CallbackQuery query, CancellationToken ct)
    {
        var data = query.Data ?? string.Empty;
        await botClient.AnswerCallbackQuery(query.Id, cancellationToken: ct);

        var convo = await stateStore.GetAsync(context.ChatId, ct);

        if (data.StartsWith("quiz:open:"))
        {
            await quizMenuHandler.ShowQuizDetailsAsync(context, Guid.Parse(data["quiz:open:".Length..]), ct);
        }
        else if (data == "quiz:create")
        {
            await createQuizHandler.StartAsync(context, ct);
        }
        else if (data.StartsWith("quiz:addq:"))
        {
            convo.Flow = ConversationFlow.AddingQuestion;
            convo.Step = ConversationStep.QuestionText;
            convo.Data.QuizSetId = Guid.Parse(data["quiz:addq:".Length..]);
            await stateStore.SetAsync(context.ChatId, convo, ct);
            await botClient.SendMessage(context.ChatId, "Текст питання:", cancellationToken: ct);
        }
        else if (data.StartsWith("quiz:publish:"))
        {
            await quizMenuHandler.PublishAsync(context, Guid.Parse(data["quiz:publish:".Length..]), publish: true, ct);
        }
        else if (data.StartsWith("quiz:unpublish:"))
        {
            await quizMenuHandler.PublishAsync(context, Guid.Parse(data["quiz:unpublish:".Length..]), publish: false, ct);
        }
        else if (data.StartsWith("quiz:delete:"))
        {
            await quizMenuHandler.ConfirmDeleteAsync(context, Guid.Parse(data["quiz:delete:".Length..]), ct);
        }
        else if (data.StartsWith("quiz:delete_confirm:"))
        {
            await quizMenuHandler.DeleteAsync(context, Guid.Parse(data["quiz:delete_confirm:".Length..]), ct);
        }
        else if (data == "quiz:delete_cancel")
        {
            await botClient.SendMessage(context.ChatId, "Скасовано.", cancellationToken: ct);
        }
        else if (data.StartsWith("qtype:"))
        {
            var type = (QuestionType)int.Parse(data["qtype:".Length..]);
            await createQuizHandler.HandleQuestionTypeChosenAsync(context, convo, type, ct);
        }
        else if (data is "opt:correct" or "opt:incorrect")
        {
            await createQuizHandler.HandleOptionCorrectnessChosenAsync(context, convo, data == "opt:correct", ct);
        }
        else if (data == "opt:more")
        {
            await createQuizHandler.HandleAddAnotherOptionAsync(context, convo, ct);
        }
        else if (data == "opt:finish")
        {
            await createQuizHandler.HandleFinishQuestionAsync(context, convo, ct);
        }
        else if (data == "quiz:nextquestion")
        {
            await createQuizHandler.StartNextQuestionAsync(context, convo, ct);
        }
        else if (data == "quiz:finish")
        {
            await createQuizHandler.FinishAsync(context, convo, ct);
        }
        else if (data.StartsWith("catalog:page:"))
        {
            await quizMenuHandler.ShowPublicCatalogAsync(context, int.Parse(data["catalog:page:".Length..]), ct);
        }
        else if (data.StartsWith("catalog:open:"))
        {
            await quizMenuHandler.ShowQuizDetailsAsync(context, Guid.Parse(data["catalog:open:".Length..]), ct);
        }
        else if (data.StartsWith("room:create:"))
        {
            await gameRoomHandler.CreateRoomAsync(context, Guid.Parse(data["room:create:".Length..]), ct);
        }
        else if (data.StartsWith("answer:"))
        {
            await gameLiveHandler.HandleAnswerCallbackAsync(context, data, ct);
        }
        else if (data.StartsWith("powerup:"))
        {
            var powerUp = (PowerUpType)int.Parse(data["powerup:".Length..]);
            await gameLiveHandler.HandlePowerUpCallbackAsync(context, powerUp, ct);
        }
        else if (data == "host:next")
        {
            await gameLiveHandler.HandleHostNextQuestionAsync(context, ct);
        }
        else if (data == "host:end")
        {
            await gameLiveHandler.HandleHostEndGameAsync(context, ct);
        }
        else if (data == "broadcast:confirm")
        {
            await adminHandler.ConfirmBroadcastAsync(context, convo, ct);
        }
        else if (data == "broadcast:cancel")
        {
            await adminHandler.CancelBroadcastAsync(context, ct);
        }
    }
}
