using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Features.Auth.Common;
using QuizArena.Application.Features.Auth.Events;
using QuizArena.Application.Features.GameRooms.Events;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IAppDbContext dbContext,
    IValidator<RegisterCommand> validator,
    ITokenService tokenService,
    IPublisher publisher)
: ICommandHandler<RegisterCommand, ErrorOr<TokenPair>>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    
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
        await dbContext.SaveChangesAsync(ct);
        
        var accessToken = tokenService.GenerateAccessToken(userIdResult.Value);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = TokenHasher.Hash(refreshToken);

        await identityService.StoreRefreshTokenAsync(userIdResult.Value, refreshTokenHash, RefreshTokenLifetime, ct);

        await publisher.Publish(new UserRegisteredNotification(userIdResult.Value, command.Email, command.NickName), ct);
        
        return new TokenPair(accessToken, refreshToken);
    }
}