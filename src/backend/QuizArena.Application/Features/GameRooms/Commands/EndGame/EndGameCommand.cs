using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.GameRooms.Commands.EndGame;

public sealed record EndGameCommand(string RoomCode) : ICommand<ErrorOr<Updated>>;