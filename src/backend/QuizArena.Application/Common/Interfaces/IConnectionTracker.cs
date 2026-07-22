namespace QuizArena.Application.Common.Interfaces;

public interface IConnectionTracker
{
    Task RegisterConnectionAsync(Guid participantId, string connectionId, CancellationToken ct = default);
    Task<string?> GetConnectionAsync(Guid participantId, CancellationToken ct = default);
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default);
    Task<Guid?> GetParticipantIdByConnectionAsync(string connectionId, CancellationToken ct = default);
}