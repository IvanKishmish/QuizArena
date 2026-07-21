using QuizArena.Application.Common.Interfaces;
using QuizArena.WebApi.Hubs;
using QuizArena.WebApi.Services;

namespace QuizArena.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddSignalR();

        services.AddScoped<IGameNotifier, GameNotifier>();
        
        services.AddHttpClient<IEmailSender, ResendEmailSender>();

        return services;
    }
}