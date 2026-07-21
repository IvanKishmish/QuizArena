namespace QuizArena.Persistence.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; private set; }
    
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
    
    private RefreshToken()
    {} // ef

    public static RefreshToken Create(Guid userId, string tokenHash, TimeSpan lifetime)
        => new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime)
        };
    
    public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;
}