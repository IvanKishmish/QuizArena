using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Features.GameRooms.Commands.EndGame;

public sealed class EndGameCommandValidator : AbstractValidator<EndGameCommand>
{
    public EndGameCommandValidator()
    {
        RuleFor(x => x.RoomCode).SetRoomCode();
    }
}