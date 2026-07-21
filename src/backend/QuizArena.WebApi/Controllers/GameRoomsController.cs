using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.GameRooms.Commands.CreateGameRoom;
using QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;
using QuizArena.Application.Features.GameRooms.Commands.StartGame;
using QuizArena.WebApi.Contracts.GameRooms;

namespace QuizArena.WebApi.Controllers;

public sealed class GameRoomsController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateGameRoomCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }
    
    [HttpPost("{roomCode}/join")]
    public async Task<IActionResult> Join(string roomCode, JoinGameRoomRequest request, CancellationToken ct = default)
    {
        var command = new JoinGameRoomCommand(roomCode, request.DisplayName);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPost("{roomCode}/start")]
    [Authorize]
    public async Task<IActionResult> Start(string roomCode, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new StartGameCommand(roomCode), ct);
        return HandleResult(result);
    }
}