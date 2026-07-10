using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Persistence.Context;
using QuizArena.Persistence.Identity;
using QuizArena.Persistence.Interceptors;
using QuizArena.Persistence.Mongo;

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
        
        services.AddSingleton<IMongoDatabase>(_ =>
        {
            var mongoConnectionString = configuration["Mongo:ConnectionString"] 
                                        ?? throw new InvalidOperationException("Mongo connection string not found.");
            var mongoDatabaseName = configuration["Mongo:DatabaseName"]
                               ?? throw new InvalidOperationException("Mongo database name not found.");
            
            var client = new MongoClient(mongoConnectionString);
            return client.GetDatabase(mongoDatabaseName);
        });
        
        MongoClassMapConfiguration.Configure();
        
        services.AddSingleton<IQuestionStore, QuestionStore>();

        return services;
    }
}