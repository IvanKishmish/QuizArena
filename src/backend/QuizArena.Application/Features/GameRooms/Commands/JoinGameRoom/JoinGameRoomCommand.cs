using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;

public sealed record JoinGameRoomCommand(string RoomCode, string DisplayName)
    : ICommand<ErrorOr<Guid>>;