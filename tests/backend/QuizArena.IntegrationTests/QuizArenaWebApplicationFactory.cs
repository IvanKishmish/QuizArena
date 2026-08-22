using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace QuizArena.IntegrationTests;

public sealed class QuizArenaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
        => await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _redis.StartAsync());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DB_CONNECTION_STRING"] = _postgres.GetConnectionString(),
            ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
            ["Mongo:DatabaseName"] = "quizarena_integration_tests",
            ["Redis:ConnectionString"] = _redis.GetConnectionString(),
            ["SignalR:UseRedisBackplane"] = "false",

            ["Jwt:Secret"] = "integration-test-signing-key-needs-to-be-long-enough-for-hmacsha256",
            ["Jwt:Issuer"] = "QuizArena.IntegrationTests",
            ["Jwt:Audience"] = "QuizArena.IntegrationTests",
            ["Jwt:ExpiryMinutes"] = "15",

            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "2525",
            ["Smtp:Username"] = "test",
            ["Smtp:Password"] = "test",
            ["Smtp:FromAddress"] = "noreply@quizarena.test",

            ["App:BaseUrl"] = "https://quizarena.test",
            ["CORS_ALLOWED_ORIGINS"] = "https://quizarena.test"
        };

        foreach (var (key, value) in settings)
        {
            if (value is not null)
                builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(settings));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }
}
