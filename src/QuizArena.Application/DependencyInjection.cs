using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Application.Features.Auth.Register;

namespace QuizArena.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
        
        return services;
    }
}