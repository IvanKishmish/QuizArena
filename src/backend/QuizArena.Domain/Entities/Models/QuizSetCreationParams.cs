namespace QuizArena.Domain.Entities.Models;

public sealed record QuizSetCreationParams(
    Guid OwnerId,
    string Title,
    string Description);