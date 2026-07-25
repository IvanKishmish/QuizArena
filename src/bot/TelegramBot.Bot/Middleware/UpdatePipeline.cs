namespace TelegramBot.Bot.Middleware;

public sealed class UpdatePipeline(IEnumerable<IUpdateMiddleware> middlewares)
{
    private readonly IReadOnlyList<IUpdateMiddleware> _middlewares = middlewares.ToList();

    public async Task RunAsync(UpdateContext context, Func<UpdateContext, CancellationToken, Task> terminal, CancellationToken ct)
    {
        await InvokeAt(0, context, terminal, ct);
    }

    private async Task InvokeAt(int index, UpdateContext context, Func<UpdateContext, CancellationToken, Task> terminal, CancellationToken ct)
    {
        if (context.ShortCircuited)
            return;

        if (index >= _middlewares.Count)
        {
            await terminal(context, ct);
            return;
        }

        await _middlewares[index].InvokeAsync(context, () => InvokeAt(index + 1, context, terminal, ct), ct);
    }
}
