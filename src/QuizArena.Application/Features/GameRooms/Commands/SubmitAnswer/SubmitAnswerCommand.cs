using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;

public sealed record SubmitAnswerCommand(
    string RoomCode,
    Guid ParticipantId,
    Guid QuestionId,
    IReadOnlyList<int> SelectedOptionIndices) : ICommand<ErrorOr<int>>;