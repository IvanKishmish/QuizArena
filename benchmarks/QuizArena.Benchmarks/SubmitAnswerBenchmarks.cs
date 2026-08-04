using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Application.Benchmarking.H1;
using QuizArena.Benchmarks.Fixtures;
using QuizArena.Benchmarks.Variants.UsingImmediateHandlers;
using QuizArena.Benchmarks.Variants.UsingMediatR;

namespace QuizArena.Benchmarks;

[MemoryDiagnoser(displayGenColumns: true)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class SubmitAnswerBenchmarks
{
    // Рівні одночасного навантаження - реальні гравці відповідають конкурентно.
    // ReSharper/Rider попереджає "setter never used" - це хибне спрацювання,
    // BenchmarkDotNet присвоює значення через reflection під час запуску.
    [Params(1, 50, 200)]
    public int ConcurrentRequests { get; set; }

    private BenchmarkFixture _fixture = null!;
    private Mediator.IMediator _mediator = null!;
    private MediatR.ISender _mediatR = null!;
    private SubmitAnswerImmediateHandler.Handler _immediateHandler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new BenchmarkFixture();
        _mediator = _fixture.MediatorProvider.GetRequiredService<Mediator.IMediator>();
        _mediatR = _fixture.MediatRProvider.GetRequiredService<MediatR.ISender>();
        _immediateHandler = _fixture.ImmediateHandlersProvider
            .GetRequiredService<SubmitAnswerImmediateHandler.Handler>();
    }

    [Benchmark(Baseline = true)]
    public async Task MediatR()
    {
        var input = _fixture.BuildInput();
        var tasks = new Task[ConcurrentRequests];
        for (var i = 0; i < ConcurrentRequests; i++)
            tasks[i] = _mediatR.Send(new SubmitAnswerMediatRCommand(input));
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Mediator()
    {
        var input = _fixture.BuildInput();
        var tasks = new ValueTask<ErrorOr.ErrorOr<int>>[ConcurrentRequests];
        for (var i = 0; i < ConcurrentRequests; i++)
            tasks[i] = _mediator.Send(new SubmitAnswerMediatorCommand(input));
        foreach (var t in tasks)
            await t;
    }

    [Benchmark]
    public async Task ImmediateHandlers()
    {
        var input = _fixture.BuildInput();
        var tasks = new Task[ConcurrentRequests];
        for (var i = 0; i < ConcurrentRequests; i++)
            tasks[i] = _immediateHandler.HandleAsync(input, CancellationToken.None).AsTask();
        await Task.WhenAll(tasks);
    }
}
