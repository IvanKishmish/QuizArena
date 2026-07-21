using Mediator;
using Microsoft.AspNetCore.SignalR;
using QuizArena.Application.Features.GameRooms.Commands.EndGame;
using QuizArena.Application.Features.GameRooms.Commands.NextQuestion;
using QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;

namespace QuizArena.WebApi.Hubs;

public sealed class GameHub(IMediator mediator) : Hub
{
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
}