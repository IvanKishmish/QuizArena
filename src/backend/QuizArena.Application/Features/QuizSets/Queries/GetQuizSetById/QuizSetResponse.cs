namespace QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;

public sealed record QuizSetResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Description,
    string Visibility);