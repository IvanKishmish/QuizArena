using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Options;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    IValidator<RefreshTokenCommand> validator)
: ICommandHandler<RefreshTokenCommand, ErrorOr<TokenPair>>
{
    public async ValueTask<ErrorOr<TokenPair>> Handle(RefreshTokenCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var incomingHash = TokenHasher.Hash(command.RefreshToken);
        var lookup = await identityService.FindRefreshTokenAsync(incomingHash, ct);

        if (lookup is null || lookup.Status == RefreshTokenStatus.Expired)
            return Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");

        if (lookup.Status == RefreshTokenStatus.Revoked)
        {
            await identityService.RevokeRefreshTokenFamilyAsync(lookup.FamilyId, ct);

            return Error.Unauthorized("Auth.RefreshTokenReuseDetected",
                "This refresh token was already used. All sessions for this account have been signed out for safety.");
        }

        if (await identityService.IsUserLockedOutAsync(lookup.UserId, ct))
        {
            await identityService.RevokeRefreshTokenFamilyAsync(lookup.FamilyId, ct);
            return Error.Forbidden("Auth.UserLockedOut", "This account is currently locked out.");
        }

        await identityService.RevokeRefreshTokenAsync(incomingHash, ct);

        var roles = await identityService.GetUserRolesAsync(lookup.UserId, ct);
        var emailsByUserId = await identityService.GetEmailsAsync([lookup.UserId], ct);
        emailsByUserId.TryGetValue(lookup.UserId, out var email);

        var newAccessToken = tokenService.GenerateAccessToken(lookup.UserId, email, roles);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = TokenHasher.Hash(newRefreshToken);

        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenExpiryDays);

        await identityService
            .StoreRefreshTokenAsync(lookup.UserId, lookup.FamilyId, newRefreshTokenHash, refreshLifetime, ct);

        return new TokenPair(newAccessToken, newRefreshToken);
    }
}
