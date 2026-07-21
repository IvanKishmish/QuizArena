using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.WebApi.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                
            return Guid.TryParse(sub, out var guid) ? guid : null;
        }
    }

    public string? Email => httpContextAccessor.HttpContext?.User
        .FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    
    public string? Role => httpContextAccessor.HttpContext?.User
        .FindFirst(ClaimTypes.Role)?.Value;
}