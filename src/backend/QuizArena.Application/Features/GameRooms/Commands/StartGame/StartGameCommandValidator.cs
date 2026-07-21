using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Features.GameRooms.Commands.StartGame;

public sealed class StartGameCommandValidator : AbstractValidator<StartGameCommand> 
{
    public StartGameCommandValidator()
    {
        RuleFor(x => x.RoomCode).SetRoomCode();
    }
}