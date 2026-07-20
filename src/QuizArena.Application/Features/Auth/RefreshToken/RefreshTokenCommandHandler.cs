using Mediator;
using ErrorOr;
using FluentValidation;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IValidator<RefreshTokenCommand> validator)
: ICommandHandler<RefreshTokenCommand, ErrorOr<TokenPair>>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    
    public async ValueTask<ErrorOr<TokenPair>> Handle(RefreshTokenCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        var incomingHash = TokenHasher.Hash(command.RefreshToken);
        
        var userId = await identityService.ValidateRefreshTokenAsync(incomingHash, ct);

        if (userId is null)
            return Error.NotFound("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");

        await identityService.RevokeRefreshTokenAsync(incomingHash, ct);

        var newAccessToken = tokenService.GenerateAccessToken(userId.Value);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = TokenHasher.Hash(newRefreshToken);

        await identityService.StoreRefreshTokenAsync(userId.Value, newRefreshTokenHash, RefreshTokenLifetime, ct);
        
        return new TokenPair(newAccessToken, newRefreshToken);
    }
}