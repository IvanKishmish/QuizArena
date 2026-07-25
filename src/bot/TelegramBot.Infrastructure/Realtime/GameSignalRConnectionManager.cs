using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramBot.Application.Contracts.Api;
using TelegramBot.Application.Interfaces;
using TelegramBot.Infrastructure.TokenStore;

namespace TelegramBot.Infrastructure.Realtime;

public sealed class BotOptions
{
    public string QuizArenaBaseUrl { get; set; } = "http://localhost:5000";
}

/// <summary>
/// One HubConnection per chatId that is currently inside a live game — never shared
/// across chats, so a chat's connection carries its own participant identity and can be
/// disposed independently the moment that chat's game ends or the chat goes idle.
/// Registered as a singleton in the host (see DependencyInjection.cs) so the dictionary
/// survives across scoped update-handling requests.
///
/// Takes IServiceScopeFactory rather than EfTokenStore directly: EfTokenStore is scoped
/// (it ultimately holds a BotDbContext, which must never be shared across concurrent
/// requests), and a singleton is not allowed to capture a scoped dependency in its
/// constructor — the DI container's own validation rejects that at startup ("Cannot
/// consume scoped service ... from singleton ..."), which is exactly what crashed the
/// container. Instead, ConnectAsync opens a short-lived scope only for the single token
/// lookup it needs, then disposes it immediately — the long-lived HubConnection itself
/// never touches EF Core again after that point.
/// </summary>
public sealed class GameSignalRConnectionManager : IGameLiveConnectionManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, HubConnection> _connections = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BotOptions _options;
    private readonly ILogger<GameSignalRConnectionManager> _logger;

    public GameSignalRConnectionManager(IServiceScopeFactory scopeFactory, IOptions<BotOptions> options, ILogger<GameSignalRConnectionManager> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public int ActiveConnectionCount => _connections.Count;

    public async Task ConnectAsync(long chatId, string roomCode, Guid participantId, GameEventCallbacks callbacks, CancellationToken ct)
    {
        await DisconnectAsync(chatId, ct); // one live game per chat at a time

        string? accessToken;
        using (var scope = _scopeFactory.CreateScope())
        {
            var tokenStore = scope.ServiceProvider.GetRequiredService<EfTokenStore>();
            (accessToken, _, _, _) = await tokenStore.GetDecryptedTokensAsync(chatId, ct);
        }

        var connection = new HubConnectionBuilder()
            .WithUrl($"{_options.QuizArenaBaseUrl}/hubs/game", options =>
            {
                // Guests (no QuizArena account) simply have no access token to attach —
                // the hub already allows anonymous join per the spec.
                if (accessToken is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        Wire(connection, callbacks);

        connection.Reconnected += async _ =>
        {
            // On reconnect the hub replays GameStateRestored itself once we rejoin the
            // group/register — do both again so the client doesn't have to guess.
            await connection.InvokeAsync("JoinRoomGroup", roomCode, ct);
            await connection.InvokeAsync("RegisterParticipant", roomCode, participantId, ct);
        };

        connection.Closed += error =>
        {
            _logger.LogInformation(error, "SignalR connection closed for chat {ChatId}", chatId);
            return callbacks.OnDisconnected?.Invoke(error) ?? Task.CompletedTask;
        };

        await connection.StartAsync(ct);
        _connections[chatId] = connection;

        await connection.InvokeAsync("JoinRoomGroup", roomCode, ct);
        await connection.InvokeAsync("RegisterParticipant", roomCode, participantId, ct);
    }

    private static void Wire(HubConnection connection, GameEventCallbacks callbacks)
    {
        if (callbacks.OnQuestionStarted is not null)
            connection.On<QuestionStartedEvent>("QuestionStarted", e => callbacks.OnQuestionStarted(e));

        if (callbacks.OnAnswerResult is not null)
            connection.On<AnswerResultEvent>("AnswerResult", e => callbacks.OnAnswerResult(e));

        if (callbacks.OnLeaderboardUpdated is not null)
            connection.On<LeaderboardUpdatedEvent>("LeaderboardUpdated", e => callbacks.OnLeaderboardUpdated(e));

        if (callbacks.OnGameFinished is not null)
            connection.On<GameFinishedEvent>("GameFinished", e => callbacks.OnGameFinished(e));

        if (callbacks.OnParticipantJoined is not null)
            connection.On<ParticipantJoinedEvent>("ParticipantJoined", e => callbacks.OnParticipantJoined(e));

        if (callbacks.OnPowerUpUsed is not null)
            connection.On<PowerUpUsedEvent>("PowerUpUsed", e => callbacks.OnPowerUpUsed(e));

        if (callbacks.OnGameStateRestored is not null)
            connection.On<GameStateRestoredEvent>("GameStateRestored", e => callbacks.OnGameStateRestored(e));
    }

    public Task JoinRoomGroupAsync(long chatId, CancellationToken ct) =>
        Task.CompletedTask; // already performed as part of ConnectAsync/reconnect; kept on the interface for callers that want to be explicit

    public Task RegisterParticipantAsync(long chatId, string roomCode, Guid participantId, CancellationToken ct) =>
        WithConnection(chatId, c => c.InvokeAsync("RegisterParticipant", roomCode, participantId, ct));

    public Task NextQuestionAsync(long chatId, string roomCode, CancellationToken ct) =>
        WithConnection(chatId, c => c.InvokeAsync("NextQuestion", roomCode, ct));

    public Task SubmitAnswerAsync(long chatId, string roomCode, Guid participantId, Guid questionId, int[] selectedOptionIndices, CancellationToken ct) =>
        WithConnection(chatId, c => c.InvokeAsync("SubmitAnswer", roomCode, participantId, questionId, selectedOptionIndices, ct));

    public Task EndGameAsync(long chatId, string roomCode, CancellationToken ct) =>
        WithConnection(chatId, c => c.InvokeAsync("EndGame", roomCode, ct));

    public Task UsePowerUpAsync(long chatId, string roomCode, Guid participantId, PowerUpType powerUpType, Guid? targetParticipantId, CancellationToken ct) =>
        WithConnection(chatId, c => c.InvokeAsync("UsePowerUp", roomCode, participantId, (int)powerUpType, targetParticipantId, ct));

    private Task WithConnection(long chatId, Func<HubConnection, Task> action)
    {
        if (!_connections.TryGetValue(chatId, out var connection))
            throw new InvalidOperationException($"No active game connection for chat {chatId}.");
        return action(connection);
    }

    public async Task DisconnectAsync(long chatId, CancellationToken ct)
    {
        if (_connections.TryRemove(chatId, out var connection))
        {
            try { await connection.StopAsync(ct); }
            finally { await connection.DisposeAsync(); }
        }
    }

    public async Task DisconnectAllAsync(CancellationToken ct)
    {
        var chatIds = _connections.Keys.ToArray();
        foreach (var chatId in chatIds)
            await DisconnectAsync(chatId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync(CancellationToken.None);
    }
}