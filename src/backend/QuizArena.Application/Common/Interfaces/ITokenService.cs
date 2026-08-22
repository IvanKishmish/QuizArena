namespace QuizArena.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string? email, IReadOnlyList<string> roles);
    string GenerateRefreshToken();
}