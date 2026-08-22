using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Features.GameHistory.Queries.GetMyGameHistory;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.UnitTests.Features.GameHistory;

public class GetMyGameHistoryQueryHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IValidator<GetMyGameHistoryQuery>> _validatorMock = new();

    private GetMyGameHistoryQueryHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetMyGameHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private async Task SeedHistoryEntry(Guid? participantUserId, Guid quizSetId, string displayName, int score, int placement)
    {
        var entry = GameHistoryEntry.Create(new GameHistoryEntryCreationParams(
            Guid.CreateVersion7(), quizSetId, participantUserId, displayName, score, placement)).Value;
        DbContext.GameHistory.Add(entry);
        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        SetupValidatorSuccess();
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
        SetupValidatorSuccess();
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
        SetupValidatorSuccess();
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
        SetupValidatorSuccess();
        var myId = Guid.CreateVersion7();
        var deletedQuizSetId = Guid.CreateVersion7(); // deliberately never seeded into DbContext.QuizSets
        await SeedHistoryEntry(myId, deletedQuizSetId, "Ivan", 50, 3);
        CurrentUserMock.Setup(x => x.UserId).Returns(myId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMyGameHistoryQuery());

        // Assert
        // S13: this placeholder used to be the single Ukrainian string ("Видалений квіз") in an otherwise
        // all-English codebase — such things don't belong in Application, and it's now consistent English.
        result.Value.Items.Single().QuizSetTitle.Should().Be("Deleted quiz");
    }

    [Fact]
    public async Task Handle_WhenUserHasNoHistory_ReturnsEmptyPagedResponse()
    {
        SetupValidatorSuccess();
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyGameHistoryQuery());

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutQueryingDatabase()
    {
        // W8
        var failure = new ValidationFailure("PageSize", "PageSize must be between 1 and 100.");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetMyGameHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyGameHistoryQuery(PageNumber: 1, PageSize: 1_000_000));

        result.IsError.Should().BeTrue();
    }
}
