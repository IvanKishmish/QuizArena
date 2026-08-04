namespace QuizArena.Application.Benchmarking.H1;

// H1 (Mediator benchmark): спільний вхідний контракт для трьох mediator-варіантів
// SubmitAnswer. Живе саме в Application (а не в Benchmarks), бо Mediator.SourceGenerator
// вже підключений до Application, і Mediator-варіант хендлера має бути в тій самій
// збірці, яку сканує генератор — інакше AddMediator() його не побачить.
public sealed record SubmitAnswerInput(
    string RoomCode,
    Guid ParticipantId,
    Guid QuestionId,
    IReadOnlyList<int> SelectedOptionIndices);
