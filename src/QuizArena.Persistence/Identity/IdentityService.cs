using ErrorOr;
using Microsoft.AspNetCore.Identity;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Persistence.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> userManager) 
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
}