using ErrorOr;
using FluentValidation;
using Immediate.Handlers.Shared;
using QuizArena.Application.Benchmarking.H1;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Benchmarks.Variants.UsingImmediateHandlers;

// НБ (важливо для розділу "Threats to validity" наукової роботи):
// Immediate.Handlers не має спільного ISender/IMediator — генерується
// клас SubmitAnswerImmediateHandler.Handler, і споживач звертається до
// нього напряму (через DI або конструктор). Тобто тут вимірюється
// не "ще один source-gen mediator", а "відсутність mediator-шару взагалі".
//
// Клас має бути static partial (не sealed!) — саме тоді Immediate.Handlers
// пересилає всі залежності як параметри в HandleAsync, а не через конструктор.
// Це ідентично патерну з PlaceBid.cs / GetUserById.cs у BidPulse.
[Handler]
public static partial class SubmitAnswerImmediateHandler
{
    private static ValueTask<ErrorOr<int>> HandleAsync(
        SubmitAnswerInput input,
        IGameRoomStore gameRoomStore,
        IQuestionStore questionStore,
        ILeaderboardStore leaderboardStore,
        IGameNotifier gameNotifier,
        IValidator<SubmitAnswerInput> validator,
        CancellationToken ct)
        => SubmitAnswerCore.ExecuteAsync(
            input, gameRoomStore, questionStore, leaderboardStore, gameNotifier, validator, ct);
}
