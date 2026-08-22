using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Options;
using QuizArena.Application.Features.Auth.Common;
using QuizArena.Application.Features.Auth.Events;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IAppDbContext dbContext,
    IOutboxWriter outboxWriter,
    IValidator<RegisterCommand> validator,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions)
: ICommandHandler<RegisterCommand, ErrorOr<TokenPair>>
{
    public async ValueTask<ErrorOr<TokenPair>> Handle(RegisterCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        var userIdResult = await identityService.CreateUserAsync(command.Email, command.Password, ct);

        if (userIdResult.IsError)
            return userIdResult.Errors;
        
        var playerResult = Player.Create(userIdResult.Value, command.NickName);
        
        if (playerResult.IsError)
            return playerResult.Errors;
        
        dbContext.Players.Add(playerResult.Value);

        outboxWriter.Enqueue(new WelcomeEmailMessage(userIdResult.Value, command.Email, command.NickName));

        await dbContext.SaveChangesAsync(ct);
        
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
