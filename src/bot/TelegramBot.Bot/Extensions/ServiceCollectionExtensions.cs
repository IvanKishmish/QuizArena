using TelegramBot.Bot.Handlers;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;
using TelegramBot.Bot.Routing;

namespace TelegramBot.Bot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBotPresentation(this IServiceCollection services)
    {
        services.AddScoped<StartCommandHandler>();
        services.AddScoped<AuthConversationHandler>();
        services.AddScoped<MenuMessenger>();
        services.AddScoped<QuizMenuHandler>();
        services.AddScoped<CreateQuizConversationHandler>();
        services.AddScoped<GameRoomHandler>();
        services.AddScoped<AdminCommandHandler>();
        services.AddScoped<UpdateRouter>();

        // Singleton: owns per-chat live-game UI state that must outlive one update's scope.
        services.AddSingleton<GameLiveHandler>();

        // Order matters: cheapest/most-restrictive checks first so a blocked chat costs as
        // little work as possible. RateLimiting -> AuthContext (needed by the checks below) ->
        // Maintenance -> BannedDetection -> [router].
        services.AddScoped<IUpdateMiddleware, RateLimitingMiddleware>();
        services.AddScoped<IUpdateMiddleware, AuthContextMiddleware>();
        services.AddScoped<IUpdateMiddleware, MessageLoggingMiddleware>();
        services.AddScoped<IUpdateMiddleware, MaintenanceModeMiddleware>();
        services.AddScoped<IUpdateMiddleware, BannedAccountDetectionMiddleware>();
        services.AddScoped<UpdatePipeline>();

        return services;
    }
}
