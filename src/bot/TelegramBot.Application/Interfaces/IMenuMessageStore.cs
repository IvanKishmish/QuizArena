namespace TelegramBot.Application.Interfaces;

public interface IMenuMessageStore
{
    Task<int?> GetAsync(long chatId, CancellationToken ct);
    Task SetAsync(long chatId, int messageId, CancellationToken ct);
    Task ClearAsync(long chatId, CancellationToken ct);
}
