using Mediator;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.Auth.Login;
using QuizArena.Application.Features.Auth.RefreshToken;
using QuizArena.Application.Features.Auth.Register;

namespace QuizArena.WebApi.Controllers;

public sealed class AuthController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        
        if (result.IsError)
            return HandleResult(result);
        
        SetRefreshTokenCookie(result.Value.RefreshToken);
        
        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);

        if (result.IsError)
            return HandleResult(result);
        
        SetRefreshTokenCookie(result.Value.RefreshToken);

        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct = default)
    {
        var refreshToken = Request.Cookies["refresh_token"];
        
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized();
        
        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken), ct);
        
        if (result.IsError)
            return HandleResult(result);
        
        SetRefreshTokenCookie(result.Value.RefreshToken);

        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}