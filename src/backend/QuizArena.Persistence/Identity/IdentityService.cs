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
        
        if(await userManager.IsLockedOutAsync(user))
        {
            var isBanned = user.LockoutEnd == DateTimeOffset.MaxValue;
            return isBanned
                ? Error.Forbidden("Auth.UserBanned", "This account has been banned.")
                : Error.Forbidden("Auth.TemporarilyLocked", $"Too many failed attempts. Try again after {user.LockoutEnd:u}.");
        }
        
        var isValid = await userManager.CheckPasswordAsync(user, password);
        if (!isValid)
        {
            await userManager.AccessFailedAsync(user);
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        
        return user.Id;
    }

    public async Task StoreRefreshTokenAsync(
        Guid userId, Guid familyId, string refreshTokenHash, TimeSpan lifetime, CancellationToken ct = default)
    {
        var refreshToken = RefreshToken.Create(userId, familyId, refreshTokenHash, lifetime);
        
        identityDbContext.RefreshTokens.Add(refreshToken);
        
        await identityDbContext.SaveChangesAsync(ct);
    }

    public async Task<RefreshTokenLookup?> FindRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        var token = await identityDbContext
            .RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == refreshTokenHash, ct);

        if (token is null)
            return null;

        var status = token.RevokedAt is not null
            ? RefreshTokenStatus.Revoked
            : token.ExpiresAt <= DateTimeOffset.UtcNow
                ? RefreshTokenStatus.Expired
                : RefreshTokenStatus.Active;

        return new RefreshTokenLookup(token.UserId, token.FamilyId, status);
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

    public async Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken ct = default)
    {
        var tokens = await identityDbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.Revoke();

        await identityDbContext.SaveChangesAsync(ct);
    }

    public async Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await identityDbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.Revoke();

        await identityDbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> IsUserLockedOutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && await userManager.IsLockedOutAsync(user);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, string>();
        
        return await userManager.Users
            .Where(u => userIds.Contains(u.Id) && u.Email != null)
            .ToDictionaryAsync(u => u.Id, u => u.Email!, ct);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return [];
        
        var roles = await userManager.GetRolesAsync(user);

        return roles.ToList();
    }

    public async Task<ErrorOr<Updated>> BanUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        
        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");
        
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        await RevokeAllRefreshTokensForUserAsync(userId, ct);

        return Result.Updated;
    }

    public async Task<ErrorOr<Updated>> UnbanUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        
        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");
        
        await userManager.SetLockoutEndDateAsync(user, null);
        
        return Result.Updated;
    }
}
