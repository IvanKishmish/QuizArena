namespace QuizArena.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, IReadOnlyList<string> roles);
    string GenerateRefreshToken();
}