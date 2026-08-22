using DotNetEnv;
using Microsoft.Extensions.Configuration;

namespace QuizArena.Persistence;

internal static class DesignTimeConfiguration
{
    public static string GetConnectionString()
    {
        Env.TraversePath().Load();

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString(ConfigurationKeys.DbConnectionString)
               ?? throw new InvalidOperationException(
                   $"""
                    DB connection string not found for design-time migrations.
                    Set it as an environment variable named 'ConnectionStrings__{ConfigurationKeys.DbConnectionString}'
                    (e.g. in a .env file reachable by walking up from the current directory, or exported
                    directly in the shell), for example:
                        ConnectionStrings__{ConfigurationKeys.DbConnectionString}=Host=localhost;Port=5432;Database=quizarena;Username=postgres;Password=postgres
                    """);
    }
}
