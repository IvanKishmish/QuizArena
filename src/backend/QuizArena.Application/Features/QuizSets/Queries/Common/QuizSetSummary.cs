using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Queries.Common;

public sealed record QuizSetSummary(
    Guid Id,
    string Title,
    string Description,
    Visibility Visibility);