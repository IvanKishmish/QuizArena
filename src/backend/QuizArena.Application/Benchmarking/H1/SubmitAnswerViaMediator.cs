using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Application.Benchmarking.H1;

// Живе тут (а не в Benchmarks), бо Mediator.SourceGenerator сканує лише
// СВОЮ збірку. Application вже підключає генератор для продакшн-хендлерів,
// тож цей handler просто "їде" на тій самій реєстрації AddMediator().
public sealed record SubmitAnswerMediatorCommand(SubmitAnswerInput Input) : ICommand<ErrorOr<int>>;

public sealed class SubmitAnswerMediatorHandler(
    IGameRoomStore gameRoomStore,
    IQuestionStore questionStore,
    ILeaderboardStore leaderboardStore,
    IGameNotifier gameNotifier,
    IValidator<SubmitAnswerInput> validator)
    : ICommandHandler<SubmitAnswerMediatorCommand, ErrorOr<int>>
{
    public ValueTask<ErrorOr<int>> Handle(SubmitAnswerMediatorCommand command, CancellationToken ct)
        => SubmitAnswerCore.ExecuteAsync(
            command.Input, gameRoomStore, questionStore, leaderboardStore, gameNotifier, validator, ct);
}
