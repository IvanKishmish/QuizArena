using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Persistence.Context;
using QuizArena.Persistence.Identity;
using QuizArena.Persistence.Interceptors;
using QuizArena.Persistence.Mongo;
using QuizArena.Persistence.Redis;
using StackExchange.Redis;

namespace QuizArena.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DB_CONNECTION_STRING");

        services.AddScoped<UpdateAuditableEntitiesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var auditingInterceptor = sp.GetRequiredService<UpdateAuditableEntitiesInterceptor>();

            options.UseNpgsql(connectionString)
                .AddInterceptors(auditingInterceptor);
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddDbContext<ApplicationIdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
            .AddDefaultTokenProviders();
        
        services.AddScoped<IIdentityService, IdentityService>();
        
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        
        services.AddSingleton<IMongoDatabase>(_ =>
        {
            var mongoConnectionString = configuration["Mongo:ConnectionString"] 
                                        ?? throw new InvalidOperationException("Mongo connection string not found.");
            var mongoDatabaseName = configuration["Mongo:DatabaseName"]
                               ?? throw new InvalidOperationException("Mongo database name not found.");
            
            var clientSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);

            var client = new MongoClient(clientSettings);
            return client.GetDatabase(mongoDatabaseName);
        });
        
        MongoClassMapConfiguration.Configure();
        
        services.AddSingleton<IQuestionStore, QuestionStore>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConnectionString = configuration["Redis:ConnectionString"]
                ?? throw new InvalidOperationException("Redis connection string not found.");
            
            return ConnectionMultiplexer.Connect(redisConnectionString);
        });
        
        services.AddSingleton<IGameRoomStore, GameRoomStore>();
        services.AddScoped<IRoomCodeGenerator, RoomCodeGenerator>();
        services.AddScoped<ILeaderboardStore, LeaderboardStore>();
        services.AddScoped<IConnectionTracker, ConnectionTracker>();

        return services;
    }
}