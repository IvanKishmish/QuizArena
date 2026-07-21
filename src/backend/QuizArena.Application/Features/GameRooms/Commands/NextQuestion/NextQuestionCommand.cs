using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.GameRooms.Commands.NextQuestion;

public sealed record NextQuestionCommand(string RoomCode) : ICommand<ErrorOr<Updated>>;