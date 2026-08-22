using FluentValidation;
using Microsoft.Extensions.Options;
using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Options;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    IValidator<LoginCommand> validator)
: ICommandHandler<LoginCommand, ErrorOr<TokenPair>>
{
    public async ValueTask<ErrorOr<TokenPair>> Handle(LoginCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var userIdResult = await identityService.ValidateCredentialsAsync(command.Email, command.Password, ct);

        if (userIdResult.IsError)
            return userIdResult.Errors;

        var roles = await identityService.GetUserRolesAsync(userIdResult.Value, ct);
        
        var accessToken = tokenService.GenerateAccessToken(userIdResult.Value, command.Email, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = TokenHasher.Hash(refreshToken);

        var familyId = Guid.CreateVersion7();
        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenExpiryDays);

        await identityService
            .StoreRefreshTokenAsync(userIdResult.Value, familyId, refreshTokenHash, refreshLifetime, ct);
        
        return new TokenPair(accessToken, refreshToken); 
    }
}
