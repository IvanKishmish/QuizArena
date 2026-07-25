using Telegram.Bot;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

public sealed class GameRoomHandler(
    ITelegramBotClient botClient,
    IConversationStateStore stateStore,
    IQuizArenaApiClient apiClient,
    ITokenStore tokenStore,
    GameLiveHandler liveHandler)
{
    public async Task CreateRoomAsync(UpdateContext context, Guid quizSetId, CancellationToken ct)
    {
        var result = await apiClient.CreateGameRoomAsync(context.ChatId, new CreateGameRoomRequest(quizSetId), ct);
        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось створити ігрову кімнату.", cancellationToken: ct);
            return;
        }

        await botClient.SendMessage(context.ChatId,
            $"Кімнату створено! Код гри:\n\n*`{result.Value!.RoomCode}`*\n\nПоділись цим кодом з друзями — вони можуть приєднатись командою /join.",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, cancellationToken: ct);

        // The creator is automatically the host; connect them to the live hub right away
        // so /nextquestion and the leaderboard show up without a separate /join.
        var joinResult = await apiClient.JoinGameRoomAsync(context.ChatId, result.Value.RoomCode, new JoinGameRoomRequest("Host"), ct);
        if (joinResult.IsSuccess)
            await liveHandler.ConnectAndEnterGameAsync(context, result.Value.RoomCode, joinResult.Value!.ParticipantId, isHost: true, ct);
    }

    public async Task StartJoinFlowAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.SetAsync(context.ChatId,
            new ConversationContext { Flow = ConversationFlow.AwaitingRoomCodeToJoin, Step = ConversationStep.RoomCode }, ct);
        await botClient.SendMessage(context.ChatId, "Введи код кімнати:", cancellationToken: ct);
    }

    public async Task HandleRoomCodeEnteredAsync(UpdateContext context, ConversationContext convo, string roomCode, CancellationToken ct)
    {
        var session = await tokenStore.GetAsync(context.ChatId, ct);

        if (context.IsAuthenticated && session?.NickName is not null)
        {
            await JoinAsync(context, roomCode, session.NickName, ct);
            return;
        }

        convo.Data.RoomCode = roomCode;
        convo.Flow = ConversationFlow.AwaitingGuestDisplayName;
        convo.Step = ConversationStep.GuestDisplayName;
        await stateStore.SetAsync(context.ChatId, convo, ct);
        await botClient.SendMessage(context.ChatId, "Як тебе представити іншим гравцям?", cancellationToken: ct);
    }

    public async Task HandleGuestDisplayNameEnteredAsync(UpdateContext context, ConversationContext convo, string displayName, CancellationToken ct)
    {
        await JoinAsync(context, convo.Data.RoomCode!, displayName, ct);
    }

    private async Task JoinAsync(UpdateContext context, string roomCode, string displayName, CancellationToken ct)
    {
        var result = await apiClient.JoinGameRoomAsync(context.ChatId, roomCode, new JoinGameRoomRequest(displayName), ct);
        await stateStore.ClearAsync(context.ChatId, ct);

        if (!result.IsSuccess)
        {
            await botClient.SendMessage(context.ChatId, "Не вдалось приєднатись — перевір код кімнати.", cancellationToken: ct);
            return;
        }

        await botClient.SendMessage(context.ChatId, $"Приєднався(-лась) як «{displayName}». Очікуй початку гри…", cancellationToken: ct);
        await liveHandler.ConnectAndEnterGameAsync(context, roomCode, result.Value!.ParticipantId, isHost: false, ct);
    }
}
