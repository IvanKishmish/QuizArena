using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Persistence.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    ApplicationIdentityDbContext identityDbContext) 
: IIdentityService
{
    public async Task<ErrorOr<Guid>> CreateUserAsync(string email, string password, CancellationToken ct = default)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if(existing is not null)
            return Error.Conflict("Auth.EmailExists", "Email is already registered.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };
        
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            return result.Errors
                .Select(e => Error.Validation(e.Code, e.Description))
                .ToList();

        return user.Id;
    }

    public async Task<ErrorOr<Guid>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        
        if(user is null)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        
        var isValid = await userManager.CheckPasswordAsync(user, password);
        
        if (!isValid)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        return user.Id;
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string refreshTokenHash, TimeSpan lifetime, CancellationToken ct = default)
    {
        var refreshToken = RefreshToken.Create(userId, refreshTokenHash, lifetime);
        
        identityDbContext.RefreshTokens.Add(refreshToken);
        
        await identityDbContext.SaveChangesAsync(ct);
    }

    public async Task<Guid?> ValidateRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        var token = await identityDbContext
            .RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash, ct);

        if (token is null || !token.IsActive)
            return null;

        return token.UserId;
    }

    public async Task RevokeRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        var token = await identityDbContext
            .RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash, ct);

        if (token is null)
            return;
        
        token.Revoke();
        
        await identityDbContext.SaveChangesAsync(ct);
    }
}