using FluentAssertions;
using QuizArena.Application.Features.GameHistory.Queries.GetMyGameHistory;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.UnitTests.Features.GameHistory;

public class GetMyGameHistoryQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetMyGameHistoryQueryHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object);

    private async Task SeedHistoryEntry(Guid? participantUserId, Guid quizSetId, string displayName, int score, int placement)
    {
        var entry = GameHistoryEntry.Create(new GameHistoryEntryCreationParams(
            quizSetId, participantUserId, displayName, score, placement)).Value;
        DbContext.GameHistory.Add(entry);
        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyGameHistoryQuery());

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
    }

    [Fact]
    public async Task Handle_ReturnsOnlyEntriesBelongingToCurrentUser()
    {
        // Arrange
        var myId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(Guid.CreateVersion7(), title: "My Quiz");
        await SeedHistoryEntry(myId, quizSet.Id, "Ivan", 100, 1);
        await SeedHistoryEntry(Guid.CreateVersion7(), quizSet.Id, "SomeoneElse", 200, 1); // another user's entry

        CurrentUserMock.Setup(x => x.UserId).Returns(myId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMyGameHistoryQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(h => h.FinalScore == 100);
    }

    [Fact]
    public async Task Handle_EnrichesEntriesWithQuizSetTitleFromQuizSetsTable()
    {
        // Arrange
        var myId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(Guid.CreateVersion7(), title: "Ukrainian History");
        await SeedHistoryEntry(myId, quizSet.Id, "Ivan", 100, 2);
        CurrentUserMock.Setup(x => x.UserId).Returns(myId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMyGameHistoryQuery());

        // Assert
        result.Value.Items.Single().QuizSetTitle.Should().Be("Ukrainian History");
    }

    [Fact]
    public async Task Handle_WhenQuizSetWasDeleted_FallsBackToPlaceholderTitle()
    {
        // Arrange: the quiz set that generated this history entry no longer exists in the QuizSets table
        var myId = Guid.CreateVersion7();
        var deletedQuizSetId = Guid.CreateVersion7(); // deliberately never seeded into DbContext.QuizSets
        await SeedHistoryEntry(myId, deletedQuizSetId, "Ivan", 50, 3);
        CurrentUserMock.Setup(x => x.UserId).Returns(myId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMyGameHistoryQuery());

        // Assert
        result.Value.Items.Single().QuizSetTitle.Should().Be("Видалений квіз");
    }

    [Fact]
    public async Task Handle_WhenUserHasNoHistory_ReturnsEmptyPagedResponse()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyGameHistoryQuery());

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
