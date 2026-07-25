using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Admin.Commands.BanUser;

public sealed class BanUserCommandHandler(IIdentityService identityService, ICurrentUserService currentUserService)
: ICommandHandler<BanUserCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(BanUserCommand command, CancellationToken ct = default)
    {
        if (command.UserId == currentUserService.UserId)
            return Error.Validation("CantBanYourself", "You cannot ban yourself.");
        
        var result = await identityService.BanUserAsync(command.UserId, ct);

        return result;
    }
}