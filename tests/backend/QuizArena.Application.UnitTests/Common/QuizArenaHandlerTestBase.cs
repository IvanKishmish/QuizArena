using Microsoft.EntityFrameworkCore;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;
using QuizArena.Persistence.Context;
using QuizArena.Persistence.Interceptors;

namespace QuizArena.Application.UnitTests.Common;

/// <summary>
/// Base class for handler tests that need a real AppDbContext (EF Core InMemory)
/// together with the same SaveChanges interceptor used in production (sets CreatedAt/CreatedBy).
/// Inherit this class instead of copying the setup into every test file.
/// </summary>
public abstract class QuizArenaHandlerTestBase : IDisposable
{
    protected readonly AppDbContext DbContext;
    protected readonly Mock<ICurrentUserService> CurrentUserMock = new();
    protected readonly Mock<IQuestionStore> QuestionStoreMock = new();

    protected QuizArenaHandlerTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .AddInterceptors(new UpdateAuditableEntitiesInterceptor(CurrentUserMock.Object))
            .Options;

        DbContext = new AppDbContext(options);
    }

    protected async Task<QuizSet> SeedQuizSet(
        Guid ownerId,
        Visibility visibility = Visibility.Private,
        string title = "Title",
        string description = "Description")
    {
        var quizSet = QuizSet.Create(new QuizSetCreationParams(ownerId, title, description)).Value;

        if (visibility == Visibility.Public)
            quizSet.Publish();

        DbContext.QuizSets.Add(quizSet);
        await DbContext.SaveChangesAsync();

        return quizSet;
    }

    protected static Question CreateValidQuestion(string text = "2 + 2 = ?")
    {
        var options = new List<AnswerOptionParams>
        {
            new("4", true, 0),
            new("5", false, 1)
        };

        return Question.Create(new QuestionCreationParams(
            text, QuestionType.SingleChoice, TimeLimitSeconds: 30, Points: 100, options)).Value;
    }

    public void Dispose() => DbContext.Dispose();
}
