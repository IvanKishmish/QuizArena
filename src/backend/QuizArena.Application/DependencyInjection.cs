using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Application.Common.Behaviours;
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
        
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        
        return services;
    }
}