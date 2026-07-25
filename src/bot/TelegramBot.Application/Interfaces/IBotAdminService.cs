namespace TelegramBot.Application.Interfaces;

public interface IBotAdminService
{
    Task<bool> IsAdminAsync(long telegramUserId, CancellationToken ct);
    Task GrantAsync(long telegramUserId, string? username, string grantedBy, CancellationToken ct);
    Task RevokeAsync(long telegramUserId, CancellationToken ct);
    Task<IReadOnlyList<long>> GetAllAdminIdsAsync(CancellationToken ct);
}

public interface IMaintenanceModeService
{
    Task<bool> IsEnabledAsync(CancellationToken ct);
    Task SetAsync(bool enabled, CancellationToken ct);
}
