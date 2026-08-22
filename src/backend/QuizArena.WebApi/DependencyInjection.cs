using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using QuizArena.Application.Common.Interfaces;
using QuizArena.WebApi.Hubs;
using QuizArena.Application.Common.Options;
using QuizArena.WebApi.Options;
using QuizArena.WebApi.Outbox;
using QuizArena.WebApi.Services;
using StackExchange.Redis;

namespace QuizArena.WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IParticipantTokenService, ParticipantTokenService>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var useBackplane = configuration.GetValue<bool?>("SignalR:UseRedisBackplane")
                           ?? throw new InvalidOperationException("SignalR:UseRedisBackplane is not configured.");

        services.AddSingleton<HubRateLimitingFilter>();

        var signalBuilder = services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;

            options.AddFilter<HubRateLimitingFilter>();
        });

        if (useBackplane)
        {
            var redisConnectionString = configuration["Redis:ConnectionString"]
                                        ?? throw new InvalidOperationException("Redis connection string not found.");
            
            signalBuilder.AddStackExchangeRedis(redisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("QuizArena");
            });
        }

        services.AddScoped<IGameNotifier, GameNotifier>();
        
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddScoped<OutboxMessageDispatcher>();
        services.AddHostedService<OutboxProcessorService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("join-room", limiterOptions =>
            {
                limiterOptions.PermitLimit = 20;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
