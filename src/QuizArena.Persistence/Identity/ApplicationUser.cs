using Microsoft.AspNetCore.Identity;

namespace QuizArena.Persistence.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset RegisteredAt { get; private init; } = DateTimeOffset.UtcNow;
}