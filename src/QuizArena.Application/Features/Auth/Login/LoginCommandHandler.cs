using FluentValidation;
using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IValidator<LoginCommand> validator)
: ICommandHandler<LoginCommand, ErrorOr<string>>
{
    public async ValueTask<ErrorOr<string>> Handle(LoginCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var userIdResult = await identityService.ValidateCredentialsAsync(command.Email, command.Password, ct);

        if (userIdResult.IsError)
            return userIdResult.Errors;

        var token = tokenService.GenerateToken(userIdResult.Value, command.Email);

        return token;
    }
}