using Mediator;
using ErrorOr;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(IIdentityService identityService)
    : ICommandHandler<LogoutCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(LogoutCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
            return Result.Success;

        var incomingHash = TokenHasher.Hash(command.RefreshToken);
        await identityService.RevokeRefreshTokenAsync(incomingHash, ct);

        return Result.Success;
    }
}