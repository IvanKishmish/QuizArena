using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TelegramBot.Application.Interfaces;
using TelegramBot.Infrastructure.Api;
using TelegramBot.Infrastructure.BotAdmin;
using TelegramBot.Infrastructure.ConversationState;
using TelegramBot.Infrastructure.Persistence;
using TelegramBot.Infrastructure.Realtime;
using TelegramBot.Infrastructure.TokenStore;

namespace TelegramBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelegramBotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BotOptions>(configuration.GetSection("QuizArenaApi"));

        services.AddDbContext<BotDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("BotDatabase")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        services.AddDataProtection()
            .PersistKeysToDbContext<BotDbContext>() // survives container restarts/redeploys
            .SetApplicationName("QuizArena.TelegramBot");

        // EfTokenStore is registered under its concrete type too, because QuizArenaApiClient
        // and GameSignalRConnectionManager need the internal GetDecryptedTokensAsync method
        // that isn't on the public ITokenStore contract (decrypted tokens must never leak
        // outside the Infrastructure boundary).
        services.AddScoped<EfTokenStore>();
        services.AddScoped<ITokenStore>(sp => sp.GetRequiredService<EfTokenStore>());

        services.AddScoped<IConversationStateStore, RedisConversationStateStore>();
        services.AddScoped<IMenuMessageStore, RedisMenuMessageStore>();
        services.AddScoped<IBotAdminService, EfBotAdminService>();
        services.AddSingleton<IMaintenanceModeService, RedisMaintenanceModeService>();
        services.AddScoped<IBotStatsService, BotStatsService>();
        services.AddScoped<IBroadcastService, BroadcastService>();

        // Singleton: must outlive individual update-handling scopes so a game connection
        // stays open between messages. Owns its own disposal (IAsyncDisposable) and is
        // stopped explicitly during graceful shutdown (see Program.cs).
        services.AddSingleton<GameSignalRConnectionManager>();
        services.AddSingleton<IGameLiveConnectionManager>(sp => sp.GetRequiredService<GameSignalRConnectionManager>());

        services.AddHttpClient<IQuizArenaApiClient, QuizArenaApiClient>((sp, client) =>
            {
                var baseUrl = configuration["QuizArenaApi:BaseUrl"] ?? "http://localhost:5000";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddPolicyHandler(PollyPolicies.RetryPolicy())
            .AddPolicyHandler(PollyPolicies.CircuitBreakerPolicy());

        return services;
    }
}