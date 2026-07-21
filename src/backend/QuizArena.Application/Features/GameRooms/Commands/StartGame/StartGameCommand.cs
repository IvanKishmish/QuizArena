using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.GameRooms.Commands.StartGame;

public sealed record StartGameCommand(string RoomCode) : ICommand<ErrorOr<Updated>>;