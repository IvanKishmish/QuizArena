using Microsoft.EntityFrameworkCore;
using QuizArena.Persistence.Context;
using QuizArena.Persistence.Identity;

namespace QuizArena.WebApi.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await appDbContext.Database.MigrateAsync();
        
        var identityDbContext = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
        await identityDbContext.Database.MigrateAsync();
    }
}