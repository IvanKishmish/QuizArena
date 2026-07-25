using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TelegramBot.Infrastructure.Persistence;

/// <summary>
/// EF Tools (`dotnet ef migrations add`, `dotnet ef database update`) look for this
/// interface before falling back to booting the real host. Without it, `dotnet ef` tries
/// to run TelegramBot.Bot's Program.cs top-to-bottom — which throws on a missing
/// Telegram:BotToken and tries to call the live Telegram API to set a webhook, neither of
/// which has anything to do with generating a migration.
///
/// Connection string resolution order, first match wins:
///   1. env var BOT_DB_MIGRATION_CONNECTION (use this for `dotnet ef` commands)
///   2. appsettings.json / appsettings.{ASPNETCORE_ENVIRONMENT}.json next to
///      TelegramBot.Bot, if present (so it stays in sync with the real app config)
///   3. a safe localhost fallback, so the command never crashes with a null
///      connection-string exception — it'll just fail to actually connect, which is a
///      clearer error than "Value cannot be null (Parameter 'connectionString')".
/// </summary>
public sealed class BotDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("BOT_DB_MIGRATION_CONNECTION")
            ?? TryReadFromBotAppsettings()
            ?? "Host=localhost;Port=5432;Database=quizarena_bot;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new BotDbContext(optionsBuilder.Options);
    }

    private static string? TryReadFromBotAppsettings()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            // ../TelegramBot.Bot relative to this project, matching the solution layout.
            var botProjectDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "TelegramBot.Bot");
            var candidates = new[]
            {
                Path.Combine(botProjectDir, $"appsettings.{env}.json"),
                Path.Combine(botProjectDir, "appsettings.json"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;

                var config = new ConfigurationBuilder()
                    .AddJsonFile(path)
                    .Build();

                var value = config.GetConnectionString("BotDatabase");
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
            // Design-time convenience only — any failure here just falls through to the
            // hardcoded fallback above instead of blocking the migration command.
        }

        return null;
    }
}
