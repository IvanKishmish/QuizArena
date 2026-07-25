using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Extensions;
using TelegramBot.Bot.Middleware;
using TelegramBot.Bot.Options;
using TelegramBot.Bot.Routing;
using TelegramBot.Infrastructure;
using TelegramBot.Infrastructure.Persistence;
using TelegramBot.Infrastructure.Realtime;

// Loads variables from a .env file into the process environment BEFORE WebApplication reads
// configuration — lets `dotnet run` locally see the exact same ConnectionStrings__*/
// Telegram__*/QuizArenaApi__* values docker-compose injects via `environment:`/`env_file:`.
// Never overwrites a variable already set in the environment, so inside a real container
// (where docker-compose already set everything) this is a no-op. Walks up from the working
// directory looking for ".env", same lookup docker-compose itself uses — one .env file at
// the repo root, next to the main docker-compose.yml, covers both backend and bot.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Logging (Serilog, same style as the backend)
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));

// ---------------------------------------------------------------- Options
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<BotAdminsOptions>(builder.Configuration.GetSection("BotAdmins"));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));

// ---------------------------------------------------------------- Telegram.Bot client
var telegramToken = builder.Configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>((httpClient, _) => new TelegramBotClient(telegramToken, httpClient));

// ---------------------------------------------------------------- App layers
builder.Services.AddTelegramBotInfrastructure(builder.Configuration);
builder.Services.AddBotPresentation();

// ---------------------------------------------------------------- Health checks (Part 5 requirement)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("BotDatabase")!, name: "bot-postgres")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, name: "redis");

var app = builder.Build();

// ---------------------------------------------------------------- Migrate + seed bot admins on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    await db.Database.MigrateAsync();

    var adminService = scope.ServiceProvider.GetRequiredService<IBotAdminService>();
    var seedIds = builder.Configuration.GetSection("BotAdmins:SeedTelegramUserIds").Get<long[]>() ?? [];
    var existingAdmins = await adminService.GetAllAdminIdsAsync(CancellationToken.None);
    if (existingAdmins.Count == 0)
    {
        foreach (var id in seedIds)
            await adminService.GrantAsync(id, username: null, grantedBy: "startup-seed", CancellationToken.None);
    }
}

app.MapHealthChecks("/health");

var telegramOptions = app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value;
var botClient = app.Services.GetRequiredService<ITelegramBotClient>();

if (telegramOptions.Mode.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(telegramOptions.WebhookPublicUrl))
        throw new InvalidOperationException("Telegram:WebhookPublicUrl is required when Telegram:Mode is Webhook.");

    await botClient.SetWebhook(
        telegramOptions.WebhookPublicUrl,
        secretToken: telegramOptions.WebhookSecretToken,
        allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery]);

    app.MapPost("/bot/webhook", async (HttpContext http, CancellationToken ct) =>
    {
        if (!string.IsNullOrEmpty(telegramOptions.WebhookSecretToken) &&
            http.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] != telegramOptions.WebhookSecretToken)
        {
            return Results.Unauthorized();
        }

        var update = await http.Request.ReadFromJsonAsync<Update>(ct);
        if (update is not null)
        {
            using var scope = http.RequestServices.CreateAsyncScope();
            await ProcessUpdateAsync(scope.ServiceProvider, update, ct);
        }

        return Results.Ok();
    });
}
else
{
    await botClient.DeleteWebhook();

    _ = Task.Run(async () =>
    {
        var receiverOptions = new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] };
        await botClient.ReceiveAsync(
            updateHandler: async (client, update, ct) =>
            {
                using var scope = app.Services.CreateAsyncScope();
                await ProcessUpdateAsync(scope.ServiceProvider, update, ct);
            },
            errorHandler: (client, exception, ct) =>
            {
                Log.Error(exception, "Polling error");
                return Task.CompletedTask;
            },
            receiverOptions: receiverOptions,
            cancellationToken: app.Lifetime.ApplicationStopping);
    });
}

// ---------------------------------------------------------------- Graceful shutdown: close every open game connection
app.Lifetime.ApplicationStopping.Register(() =>
{
    var connectionManager = app.Services.GetRequiredService<GameSignalRConnectionManager>();
    connectionManager.DisconnectAllAsync(CancellationToken.None).GetAwaiter().GetResult();
    Log.Information("Closed all live-game SignalR connections during shutdown.");
});

app.Run();

static async Task ProcessUpdateAsync(IServiceProvider services, Update update, CancellationToken ct)
{
    var (chatId, userId, username) = ExtractIdentity(update);
    if (chatId is null || userId is null)
        return;

    var context = new UpdateContext
    {
        Update = update,
        ChatId = chatId.Value,
        TelegramUserId = userId.Value,
        TelegramUsername = username
    };

    var pipeline = services.GetRequiredService<UpdatePipeline>();
    var router = services.GetRequiredService<UpdateRouter>();

    try
    {
        await pipeline.RunAsync(context, (ctx, token) => router.RouteAsync(ctx, token), ct);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled error while processing update for chat {ChatId}", chatId);
    }
}

static (long? chatId, long? userId, string? username) ExtractIdentity(Update update)
{
    if (update.Message is { } message)
        return (message.Chat.Id, message.From?.Id, message.From?.Username);

    if (update.CallbackQuery is { } query)
        return (query.Message?.Chat.Id, query.From.Id, query.From.Username);

    return (null, null, null);
}
