using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.Admin.Commands.UnbanUser;

public sealed class UnbanUserCommandHandler(IIdentityService identityService)
: ICommandHandler<UnbanUserCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(UnbanUserCommand command, CancellationToken ct = default)
    {
        var result = await identityService.UnbanUserAsync(command.UserId, ct);
        
        return result;
    }
}