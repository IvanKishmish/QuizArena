namespace QuizArena.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }

    IReadOnlyList<string> Roles { get; }
}
