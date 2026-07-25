using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

/// <summary>
/// Registered as a singleton (see ServiceCollectionExtensions) because it holds
/// per-chat UI state (which question is live, which options the player has tapped so
/// far for a MultipleChoice question, the id of the question message to edit for the
/// timer) that must survive across the scoped DI lifetime of individual update handling.
/// </summary>
public sealed class GameLiveHandler(ITelegramBotClient botClient, IGameLiveConnectionManager connectionManager)
{
    private readonly ConcurrentDictionary<long, LiveGameUiState> _state = new();

    public async Task ConnectAndEnterGameAsync(UpdateContext context, string roomCode, Guid participantId, bool isHost, CancellationToken ct)
    {
        _state[context.ChatId] = new LiveGameUiState(roomCode, participantId, isHost);

        var callbacks = new GameEventCallbacks
        {
            OnQuestionStarted = e => OnQuestionStartedAsync(context.ChatId, e),
            OnAnswerResult = e => OnAnswerResultAsync(context.ChatId, e),
            OnLeaderboardUpdated = e => OnLeaderboardUpdatedAsync(context.ChatId, e),
            OnGameFinished = e => OnGameFinishedAsync(context.ChatId, e),
            OnParticipantJoined = e => OnParticipantJoinedAsync(context.ChatId, e),
            OnPowerUpUsed = e => OnPowerUpUsedAsync(context.ChatId, e),
            OnGameStateRestored = e => OnGameStateRestoredAsync(context.ChatId, e),
            OnDisconnected = _ => Task.CompletedTask, // reconnect handled by SignalR's WithAutomaticReconnect
        };

        await connectionManager.ConnectAsync(context.ChatId, roomCode, participantId, callbacks, ct);

        if (isHost)
            await botClient.SendMessage(context.ChatId, "Ти хост цієї гри.", replyMarkup: KeyboardFactory.HostControls(), cancellationToken: ct);
    }

    // ---------------------------------------------------------------- Server -> chat

    private async Task OnQuestionStartedAsync(long chatId, QuestionStartedEvent e)
    {
        if (!_state.TryGetValue(chatId, out var ui)) return;

        ui.CurrentQuestionId = e.QuestionId;
        ui.SelectedIndices.Clear();
        var isMultiple = e.QuestionTypeRaw == (int)QuestionType.MultipleChoice;
        ui.CurrentQuestionIsMultipleChoice = isMultiple;

        var text = $"❓ *{Escape(e.Text)}*\n⏱ {e.TimeLimitSeconds} сек · 🏆 {e.Points} балів";
        var sent = await botClient.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2,
            replyMarkup: KeyboardFactory.AnswerOptions(e.Options, isMultiple));
        ui.QuestionMessageId = sent.MessageId;
    }

    private async Task OnAnswerResultAsync(long chatId, AnswerResultEvent e)
    {
        var text = e.IsCorrect
            ? $"✅ Правильно! +{e.PointsAwarded} балів (всього: {e.TotalScore})"
            : $"❌ Неправильно. (всього: {e.TotalScore})";
        await botClient.SendMessage(chatId, text);
    }

    private async Task OnLeaderboardUpdatedAsync(long chatId, LeaderboardUpdatedEvent e)
    {
        var lines = e.Standings
            .OrderBy(s => s.Rank)
            .Take(10)
            .Select(s => $"{RankEmoji(s.Rank)} {Escape(s.DisplayName)} — {s.Score}");

        await botClient.SendMessage(chatId, "📊 *Таблиця лідерів:*\n" + string.Join('\n', lines), parseMode: ParseMode.MarkdownV2);
    }

    private async Task OnGameFinishedAsync(long chatId, GameFinishedEvent e)
    {
        var top3 = e.FinalStandings.OrderBy(s => s.Rank).Take(3).ToList();
        var medals = new[] { "🥇", "🥈", "🥉" };
        var topLines = top3.Select((s, i) => $"{medals[i]} {Escape(s.DisplayName)} — {s.Score}");

        var restLines = e.FinalStandings.OrderBy(s => s.Rank).Skip(3)
            .Select(s => $"{s.Rank}. {Escape(s.DisplayName)} — {s.Score}");

        var text = "🏁 *Гру завершено\\!*\n\n" + string.Join('\n', topLines) +
                   (restLines.Any() ? "\n\n" + string.Join('\n', restLines) : "");

        await botClient.SendMessage(chatId, text, parseMode: ParseMode.MarkdownV2, replyMarkup: KeyboardFactory.Remove());

        await connectionManager.DisconnectAsync(chatId, CancellationToken.None);
        _state.TryRemove(chatId, out _);
    }

    private Task OnParticipantJoinedAsync(long chatId, ParticipantJoinedEvent e) =>
        _state.TryGetValue(chatId, out var ui) && ui.IsHost
            ? botClient.SendMessage(chatId, $"👋 {e.DisplayName} приєднався(-лась) до гри.")
            : Task.CompletedTask;

    private Task OnPowerUpUsedAsync(long chatId, PowerUpUsedEvent e) =>
        botClient.SendMessage(chatId, $"⚡ Використано power-up: {e.PowerUpType}.");

    private async Task OnGameStateRestoredAsync(long chatId, GameStateRestoredEvent e)
    {
        await botClient.SendMessage(chatId, "🔄 З'єднання відновлено.");
        if (e.CurrentQuestion is not null)
            await OnQuestionStartedAsync(chatId, e.CurrentQuestion);
    }

    // ---------------------------------------------------------------- Chat -> server (callback queries)

    public async Task HandleAnswerCallbackAsync(UpdateContext context, string data, CancellationToken ct)
    {
        if (!_state.TryGetValue(context.ChatId, out var ui) || ui.CurrentQuestionId is null) return;

        if (data == "answer:submit")
        {
            await SubmitCurrentSelectionAsync(context, ui, ct);
            return;
        }

        var index = int.Parse(data.Split(':')[1]);

        if (!ui.CurrentQuestionIsMultipleChoice)
        {
            ui.SelectedIndices.Clear();
            ui.SelectedIndices.Add(index);
            await SubmitCurrentSelectionAsync(context, ui, ct);
            return;
        }

        // MultipleChoice: toggle and wait for explicit "submit".
        if (!ui.SelectedIndices.Remove(index))
            ui.SelectedIndices.Add(index);
    }

    private async Task SubmitCurrentSelectionAsync(UpdateContext context, LiveGameUiState ui, CancellationToken ct)
    {
        await connectionManager.SubmitAnswerAsync(context.ChatId, ui.RoomCode, ui.ParticipantId, ui.CurrentQuestionId!.Value,
            ui.SelectedIndices.ToArray(), ct);
        ui.CurrentQuestionId = null;
    }

    public async Task HandlePowerUpCallbackAsync(UpdateContext context, PowerUpType powerUp, CancellationToken ct)
    {
        if (!_state.TryGetValue(context.ChatId, out var ui)) return;
        await connectionManager.UsePowerUpAsync(context.ChatId, ui.RoomCode, ui.ParticipantId, powerUp, targetParticipantId: null, ct);
    }

    public async Task HandleHostNextQuestionAsync(UpdateContext context, CancellationToken ct)
    {
        if (!_state.TryGetValue(context.ChatId, out var ui) || !ui.IsHost) return;
        await connectionManager.NextQuestionAsync(context.ChatId, ui.RoomCode, ct);
    }

    public async Task HandleHostEndGameAsync(UpdateContext context, CancellationToken ct)
    {
        if (!_state.TryGetValue(context.ChatId, out var ui) || !ui.IsHost) return;
        await connectionManager.EndGameAsync(context.ChatId, ui.RoomCode, ct);
    }

    private static string RankEmoji(int rank) => rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{rank}." };

    private static string Escape(string text) =>
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`").Replace(".", "\\.").Replace("!", "\\!").Replace("-", "\\-");

    private sealed class LiveGameUiState(string roomCode, Guid participantId, bool isHost)
    {
        public string RoomCode { get; } = roomCode;
        public Guid ParticipantId { get; } = participantId;
        public bool IsHost { get; } = isHost;
        public Guid? CurrentQuestionId { get; set; }
        public bool CurrentQuestionIsMultipleChoice { get; set; }
        public int? QuestionMessageId { get; set; }
        public HashSet<int> SelectedIndices { get; } = [];
    }
}
