namespace QuizArena.Application.Common.Interfaces;
using ErrorOr;

public interface IIdentityService
{
    Task<ErrorOr<Guid>> CreateUserAsync(string email, string password, CancellationToken ct = default);
    Task<ErrorOr<Guid>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
    Task StoreRefreshTokenAsync(Guid userId, string refreshTokenHash, TimeSpan lifetime, CancellationToken ct = default);
    Task<Guid?> ValidateRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}