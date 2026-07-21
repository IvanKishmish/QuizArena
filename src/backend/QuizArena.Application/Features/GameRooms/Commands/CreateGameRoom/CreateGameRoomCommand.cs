using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.GameRooms.Commands.CreateGameRoom;

public sealed record CreateGameRoomCommand(Guid QuizSetId)
    : ICommand<ErrorOr<string>>;