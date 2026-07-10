namespace QuizArena.Application.Common.Interfaces;
using ErrorOr;

public interface IIdentityService
{
    Task<ErrorOr<Guid>> CreateUserAsync(string email, string password, CancellationToken ct = default);
    Task<ErrorOr<Guid>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}