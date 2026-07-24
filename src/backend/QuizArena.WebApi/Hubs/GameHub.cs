using Mediator;
using Microsoft.AspNetCore.SignalR;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.GameRooms.Commands.EndGame;
using QuizArena.Application.Features.GameRooms.Commands.NextQuestion;
using QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;
using QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;
using QuizArena.Domain.Enums;

namespace QuizArena.WebApi.Hubs;

public sealed class GameHub
    (IMediator mediator, IConnectionTracker connectionTracker, IGameRoomStore gameRoomStore)
    : Hub
{
    public async Task RegisterParticipant(string roomCode, Guid participantId, CancellationToken ct = default)
    {
        await connectionTracker.RegisterConnectionAsync(participantId, Context.ConnectionId, ct);

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(roomCode, ct);

        if (gameRoom is null || gameRoom.Status != GameRoomStatus.InProgress)
            return;
        
        var elapsedSeconds = gameRoom.CurrentQuestionStartedAt is null
            ? 0
            : (DateTimeOffset.UtcNow - gameRoom.CurrentQuestionStartedAt.Value).TotalSeconds;

        await Clients.Caller.SendAsync("GameStateRestored", new
        {
            gameRoom.CurrentQuestionIndex,
            ElapsedSeconds = elapsedSeconds
        }, ct);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await connectionTracker.RemoveConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task JoinRoomGroup(string roomCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
    }

    public async Task LeaveRoomGroup(string roomCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
    }

    public async Task NextQuestion(string roomCode)
    {
        await mediator.Send(new NextQuestionCommand(roomCode));
    }

    public async Task SubmitAnswer(string roomCode, Guid participantId, Guid questionId,
        List<int> selectedOptionIndices)
    {
        var result = await mediator
            .Send(new SubmitAnswerCommand(roomCode, participantId, questionId, selectedOptionIndices));

        if (!result.IsError)
            await Clients.Caller.SendAsync("AnswerResult", new { Score = result.Value });
    }

    public async Task EndGame(string roomCode)
    {
        await mediator.Send(new EndGameCommand(roomCode));
    }

    public async Task UsePowerUp(string roomCode, Guid participantId, PowerUpType powerUpType, Guid? targetParticipantId)
    {
        await mediator.Send(new UsePowerUpCommand(roomCode, participantId, powerUpType, targetParticipantId));
    }
}