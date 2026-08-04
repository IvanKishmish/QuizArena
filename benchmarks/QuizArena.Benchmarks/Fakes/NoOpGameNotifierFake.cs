using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Benchmarks.Fakes;

public sealed class NoOpGameNotifierFake : IGameNotifier
{
    public Task QuestionStartedAsync(string roomCode, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ParticipantJoinedAsync(string roomCode, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task GameFinishedAsync(string roomCode, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task LeaderboardUpdatedAsync(string roomCode, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PowerUpUsedAsync(string roomCode, object payload, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendToParticipantAsync(string connectionId, string method, object payload, CancellationToken ct = default)
        => Task.CompletedTask;
}
