using QuizArena.Application.Common.Interfaces;
using StackExchange.Redis;

namespace QuizArena.Persistence.Redis;

public sealed class ConnectionTracker(IConnectionMultiplexer redis)
    : IConnectionTracker
{
    private static readonly TimeSpan ConnectionExpiration = TimeSpan.FromHours(4);
    
    private IDatabase Database => redis.GetDatabase();
    
    private static string ParticipantKey(Guid participantId) => $"connection:participant:{participantId}";
    private static string ConnectionKey(string connectionId) => $"connection:conn:{connectionId}";
    
    public async Task RegisterConnectionAsync(Guid participantId, string connectionId, CancellationToken ct = default)
    {
        await Database.StringSetAsync(ParticipantKey(participantId), connectionId,ConnectionExpiration);
        await Database.StringSetAsync(ConnectionKey(connectionId), participantId.ToString(),ConnectionExpiration);
    }

    public async Task<string?> GetConnectionAsync(Guid participantId, CancellationToken ct = default)
    {
        var value = await Database.StringGetAsync(ParticipantKey(participantId));
        return value.IsNullOrEmpty ? null : (string)value!;
    }

    public async Task RemoveConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var participantIdValue = await Database.StringGetAsync(ConnectionKey(connectionId));

        if (!participantIdValue.IsNullOrEmpty)
            await Database.KeyDeleteAsync(ParticipantKey(Guid.Parse((string)participantIdValue!)));
        
        await Database.KeyDeleteAsync(ConnectionKey(connectionId));
    }

    public async Task<Guid?> GetParticipantIdByConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var value = await Database.StringGetAsync(ConnectionKey(connectionId));
        return value.IsNullOrEmpty ? null : Guid.Parse((string)value!);
    }
}