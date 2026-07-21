using FluentValidation;
using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using QuizArena.Application.Common;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IValidator<LoginCommand> validator)
: ICommandHandler<LoginCommand, ErrorOr<TokenPair>>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    
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

        var accessToken = tokenService.GenerateAccessToken(userIdResult.Value);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = TokenHasher.Hash(refreshToken);

        await identityService
            .StoreRefreshTokenAsync(userIdResult.Value, refreshTokenHash, RefreshTokenLifetime, ct);
        
        return new TokenPair(accessToken, refreshToken); 
    }
}