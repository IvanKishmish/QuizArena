using Microsoft.AspNetCore.Identity;
using QuizArena.Persistence.Identity;

namespace QuizArena.WebApi.Extensions;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        
        var logger =  scope.ServiceProvider.GetRequiredService<ILogger<ApplicationUser>>();

        const string adminRole = "Admin";
        
        if(!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole<Guid>(adminRole));
        
        var adminEmail = configuration["AdminEmail"];
        var adminPassword = configuration["AdminPassword"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            logger.LogWarning("Admin credentials are not configured in .env.docker");
            return;
        }
        
        var existingAdmin =  await userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin is not null)
            return;

        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, adminRole);
            logger.LogInformation("Super admin successfully seeded.");
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to seed admin user. Errors: {Errors}", errors);
        }
    }
}