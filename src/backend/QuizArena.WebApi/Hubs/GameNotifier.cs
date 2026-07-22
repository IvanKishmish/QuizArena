using Microsoft.AspNetCore.SignalR;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.WebApi.Hubs;

public sealed class GameNotifier(IHubContext<GameHub> hubContext) : IGameNotifier
{
    public async Task QuestionStartedAsync(string roomCode, object payload, CancellationToken ct = default)
        => await hubContext.Clients.Group(roomCode).SendAsync("QuestionStarted", payload, ct);

    public async Task ParticipantJoinedAsync(string roomCode, object payload, CancellationToken ct = default)
        => await hubContext.Clients.Group(roomCode).SendAsync("ParticipantJoined", payload, ct);

    public async Task GameFinishedAsync(string roomCode, object payload, CancellationToken ct = default)
        => await hubContext.Clients.Group(roomCode).SendAsync("GameFinished", payload, ct);
    
    public async Task LeaderboardUpdatedAsync(string roomCode, object payload, CancellationToken ct = default)
        => await hubContext.Clients.Group(roomCode).SendAsync("LeaderboardUpdated", payload, ct);
    
    public async Task PowerUpUsedAsync(string roomCode, object payload, CancellationToken ct = default)
        => await hubContext.Clients.Group(roomCode).SendAsync("PowerUpUsed", payload, ct);

    public async Task SendToParticipantAsync(string connectionId, string method, object payload,
        CancellationToken ct = default)
        => await hubContext.Clients.Client(connectionId).SendAsync(method, payload, ct);
}