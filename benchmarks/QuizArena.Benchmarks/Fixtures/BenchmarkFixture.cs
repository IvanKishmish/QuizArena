using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using QuizArena.Application.Benchmarking.H1;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Benchmarks.Fakes;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Benchmarks.Fixtures;

public sealed class BenchmarkFixture
{
    public const string RoomCode = "BENCH01";

    public GameRoom GameRoom { get; }
    public Question Question { get; }
    public Guid ParticipantId { get; }

    public IServiceProvider MediatorProvider { get; }
    public IServiceProvider MediatRProvider { get; }
    public IServiceProvider ImmediateHandlersProvider { get; }

    public BenchmarkFixture()
    {
        Question = Question.Create(new QuestionCreationParams(
            "2 + 2 = ?",
            QuestionType.SingleChoice,
            TimeLimitSeconds: 20,
            Points: 100,
            Options:
            [
                new AnswerOptionParams("3", false, 0),
                new AnswerOptionParams("4", true, 1),
                new AnswerOptionParams("5", false, 2),
                new AnswerOptionParams("22", false, 3)
            ])).Value;

        var roomResult = GameRoom.Create(new GameRoomCreationParams(RoomCode, Guid.CreateVersion7(), Guid.CreateVersion7()));
        GameRoom = roomResult.Value;
        GameRoom.AddParticipant(Guid.CreateVersion7(), null, "BenchmarkPlayer");
        GameRoom.Start();

        ParticipantId = GameRoom.Participants[0].Id;

        // AddMediator() тепер однозначний: Mediator.SourceGenerator підключений
        // ЛИШЕ до Application (Benchmarks.csproj його більше не референсить),
        // тож видно рівно одну згенеровану реєстрацію.
        MediatorProvider = BuildProvider(services => services
            .AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        }));

        MediatRProvider = BuildProvider(services =>
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(BenchmarkFixture).Assembly);
            }));

        // Ім'я згенероване з короткої назви збірки QuizArena.Benchmarks -> QuizArenaBenchmarks.
        ImmediateHandlersProvider = BuildProvider(services => services.AddQuizArenaBenchmarksHandlers());
    }

    public SubmitAnswerInput BuildInput() => new(RoomCode, ParticipantId, Question.Id, [1]);

    private IServiceProvider BuildProvider(Action<IServiceCollection> registerMediator)
    {
        var services = new ServiceCollection();

        services.AddSingleton(GameRoom);
        services.AddSingleton(Question);
        services.AddSingleton<IGameRoomStore>(new InMemoryGameRoomStoreFake(GameRoom));
        services.AddSingleton<IQuestionStore>(new InMemoryQuestionStoreFake(Question));
        services.AddSingleton<ILeaderboardStore, InMemoryLeaderboardStoreFake>();
        services.AddSingleton<IGameNotifier, NoOpGameNotifierFake>();
        services.AddSingleton<IValidator<SubmitAnswerInput>, SubmitAnswerInputValidator>();

        registerMediator(services);

        return services.BuildServiceProvider();
    }
}
