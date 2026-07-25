using TelegramBot.Application.Contracts.Api;

namespace TelegramBot.Application.Interfaces;

/// <summary>
/// Owns the SignalR HubConnection(s) for chats currently inside a live game.
/// One connection per (chatId, roomCode) game session; the manager wires the hub's
/// server -> client events to the supplied callbacks so the Bot layer can turn them
/// into Telegram messages without knowing anything about SignalR.
/// </summary>
public interface IGameLiveConnectionManager
{
    Task ConnectAsync(long chatId, string roomCode, Guid participantId, GameEventCallbacks callbacks, CancellationToken ct);

    Task JoinRoomGroupAsync(long chatId, CancellationToken ct);
    Task RegisterParticipantAsync(long chatId, string roomCode, Guid participantId, CancellationToken ct);
    Task NextQuestionAsync(long chatId, string roomCode, CancellationToken ct);
    Task SubmitAnswerAsync(long chatId, string roomCode, Guid participantId, Guid questionId, int[] selectedOptionIndices, CancellationToken ct);
    Task EndGameAsync(long chatId, string roomCode, CancellationToken ct);
    Task UsePowerUpAsync(long chatId, string roomCode, Guid participantId, PowerUpType powerUpType, Guid? targetParticipantId, CancellationToken ct);

    /// <summary>Closes the connection for this chat, if any. Safe to call multiple times.
    /// Called on GameFinished, on explicit /leave, and during graceful shutdown.</summary>
    Task DisconnectAsync(long chatId, CancellationToken ct);

    /// <summary>Number of chats currently holding an open game connection — feeds /admin_stats.</summary>
    int ActiveConnectionCount { get; }

    /// <summary>Closes every open connection. Called once from the host's graceful-shutdown hook.</summary>
    Task DisconnectAllAsync(CancellationToken ct);
}

public sealed class GameEventCallbacks
{
    public Func<QuestionStartedEvent, Task>? OnQuestionStarted { get; init; }
    public Func<AnswerResultEvent, Task>? OnAnswerResult { get; init; }
    public Func<LeaderboardUpdatedEvent, Task>? OnLeaderboardUpdated { get; init; }
    public Func<GameFinishedEvent, Task>? OnGameFinished { get; init; }
    public Func<ParticipantJoinedEvent, Task>? OnParticipantJoined { get; init; }
    public Func<PowerUpUsedEvent, Task>? OnPowerUpUsed { get; init; }
    public Func<GameStateRestoredEvent, Task>? OnGameStateRestored { get; init; }
    public Func<Exception?, Task>? OnDisconnected { get; init; }
}
