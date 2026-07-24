namespace QuizArena.Application.Common.Interfaces;

public interface IGameNotifier
{
    Task QuestionStartedAsync(string roomCode, object payload, CancellationToken ct = default);
    Task ParticipantJoinedAsync(string roomCode, object payload, CancellationToken ct = default);
    Task GameFinishedAsync(string roomCode, object payload, CancellationToken ct = default);
    Task LeaderboardUpdatedAsync(string roomCode, object payload, CancellationToken ct = default);
    Task PowerUpUsedAsync(string roomCode, object payload, CancellationToken ct = default);
    Task SendToParticipantAsync(string connectionId, string method, object payload, CancellationToken ct = default);
}