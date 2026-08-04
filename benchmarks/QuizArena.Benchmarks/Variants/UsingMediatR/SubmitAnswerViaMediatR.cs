using ErrorOr;
using FluentValidation;
using MediatR;
using QuizArena.Application.Benchmarking.H1;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Benchmarks.Variants.UsingMediatR;

public sealed record SubmitAnswerMediatRCommand(SubmitAnswerInput Input) : IRequest<ErrorOr<int>>;

public sealed class SubmitAnswerMediatRHandler(
    IGameRoomStore gameRoomStore,
    IQuestionStore questionStore,
    ILeaderboardStore leaderboardStore,
    IGameNotifier gameNotifier,
    IValidator<SubmitAnswerInput> validator)
    : IRequestHandler<SubmitAnswerMediatRCommand, ErrorOr<int>>
{
    public Task<ErrorOr<int>> Handle(SubmitAnswerMediatRCommand request, CancellationToken ct)
        => SubmitAnswerCore.ExecuteAsync(
            request.Input, gameRoomStore, questionStore, leaderboardStore, gameNotifier, validator, ct).AsTask();
}
