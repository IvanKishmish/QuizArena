using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IAppDbContext dbContext,
    IValidator<RegisterCommand> validator)
: ICommandHandler<RegisterCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(RegisterCommand command, CancellationToken ct = default)
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
        
        return userIdResult.Value;
    }
}