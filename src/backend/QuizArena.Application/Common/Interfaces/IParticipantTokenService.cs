namespace QuizArena.Application.Common.Interfaces;

public interface IParticipantTokenService
{
    string GenerateToken(string roomCode, Guid participantId, Guid? userId);

    Task<ParticipantTokenValidationResult?> ValidateAsync(
        string token, string roomCode, Guid participantId, CancellationToken ct = default);
}

public sealed record ParticipantTokenValidationResult(Guid? UserId);
