using System.IdentityModel.Tokens.Jwt;
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
    (IMediator mediator, IConnectionTracker connectionTracker, IGameRoomStore gameRoomStore,
        IParticipantTokenService participantTokenService)
    : Hub
{
    private async Task<bool> IsVerifiedParticipantAsync(Guid claimedParticipantId, CancellationToken ct = default)
    {
        var actualParticipantId = await connectionTracker.GetParticipantIdByConnectionAsync(Context.ConnectionId, ct);
        return actualParticipantId == claimedParticipantId;
    }

    private async Task<bool> IsRoomHostAsync(string roomCode, CancellationToken ct = default)
    {
        var userIdClaim = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return false;

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(roomCode, ct);
        return gameRoom is not null && gameRoom.HostId == userId;
    }
    
    public async Task RegisterParticipant(string roomCode, Guid participantId, string participantToken)
    {
        var ct = Context.ConnectionAborted;

        var validation = await participantTokenService.ValidateAsync(participantToken, roomCode, participantId, ct);

        if (validation is null)
        {
            await Clients.Caller.SendAsync("Error", "Invalid or expired participant token.", ct);
            return;
        }

        if (validation.UserId is not null)
        {
            var callerUserIdClaim = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!Guid.TryParse(callerUserIdClaim, out var callerUserId) || callerUserId != validation.UserId)
            {
                await Clients.Caller.SendAsync("Error", "This participant belongs to a different account.", ct);
                return;
            }
        }

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
        var ct = Context.ConnectionAborted;
        
        if (!await IsRoomHostAsync(roomCode, ct))
        {
            await Clients.Caller.SendAsync("Error", "Only the host can advance the question.", ct);
            return;
        }

        var result = await mediator.Send(new NextQuestionCommand(roomCode), ct);

        if (result.IsError)
            await Clients.Caller.SendAsync("Error", result.FirstError.Description, ct);
    }

    public async Task SubmitAnswer(string roomCode, Guid participantId, Guid questionId,
        List<int> selectedOptionIndices)
    {
        var ct = Context.ConnectionAborted;
        
        if (!await IsVerifiedParticipantAsync(participantId, ct))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized participant.", ct);
            return;
        }

        var result = await mediator
            .Send(new SubmitAnswerCommand(roomCode, participantId, questionId, selectedOptionIndices), ct);

        if (result.IsError)
            await Clients.Caller.SendAsync("Error", result.FirstError.Description, ct);
        else
            await Clients.Caller.SendAsync("AnswerResult", new { Score = result.Value }, ct);
    }

    public async Task EndGame(string roomCode)
    {
        var ct = Context.ConnectionAborted;
        
        if (!await IsRoomHostAsync(roomCode, ct))
        {
            await Clients.Caller.SendAsync("Error", "Only the host can end the game.", ct);
            return;
        }

        var result = await mediator.Send(new EndGameCommand(roomCode), ct);

        if (result.IsError)
            await Clients.Caller.SendAsync("Error", result.FirstError.Description, ct);
    }

    public async Task UsePowerUp(string roomCode, Guid participantId, PowerUpType powerUpType,
        Guid? targetParticipantId)
    {
        var ct = Context.ConnectionAborted;
        
        if (!await IsVerifiedParticipantAsync(participantId, ct))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized participant.", ct);
            return;
        }

        var result = await mediator
            .Send(new UsePowerUpCommand(roomCode, participantId, powerUpType, targetParticipantId), ct);

        if (result.IsError)
            await Clients.Caller.SendAsync("Error", result.FirstError.Description, ct);
    }
}
