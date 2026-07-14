using Mediator;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.Auth.Login;
using QuizArena.Application.Features.Auth.Register;

namespace QuizArena.WebApi.Controllers;

public sealed class AuthController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        
        return HandleResult(result);
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
}