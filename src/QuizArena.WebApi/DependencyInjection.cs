using QuizArena.Application.Common.Interfaces;
using QuizArena.WebApi.Services;

namespace QuizArena.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}