using Microsoft.EntityFrameworkCore;
using QuizArena.Persistence.Context;

namespace QuizArena.WebApi.Outbox;

public sealed class OutboxProcessorService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxAttempts = 6; 

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processing loop failed unexpectedly");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxMessageDispatcher>();

        var now = DateTimeOffset.UtcNow;

        var batch = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.NextAttemptAt <= now && m.RetryCount < MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return;

        foreach (var message in batch)
        {
            try
            {
                await dispatcher.DispatchAsync(message.Type, message.Payload, ct);
                message.MarkProcessed();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Exponential backoff: 2, 4, 8, 16, 32 minutes. A burst of failures (e.g. SMTP server down
                // for ten minutes) doesn't turn into a tight retry loop hammering it.
                var delay = TimeSpan.FromMinutes(Math.Pow(2, message.RetryCount + 1));
                message.MarkFailed(ex.Message, delay);

                logger.LogWarning(ex,
                    "Outbox message {MessageId} of type {Type} failed (attempt {Attempt}/{MaxAttempts})",
                    message.Id, message.Type, message.RetryCount, MaxAttempts);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
