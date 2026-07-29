using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameRooms.Events;
using QuizArena.Persistence.Context;

namespace QuizArena.Application.UnitTests.Features.GameRooms.Events;

public class CleanupGameRoomHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<ILeaderboardStore> _leaderboardStoreMock = new();

    [Fact]
    public async Task Handle_DeletesRoomAndLeaderboardForFinishedGame()
    {
        // Arrange
        var handler = new CleanupGameRoomHandler(_gameRoomStoreMock.Object, _leaderboardStoreMock.Object);
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), [], new Dictionary<Guid, Guid?>());

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _gameRoomStoreMock.Verify(x => x.DeleteAsync("ABC123", It.IsAny<CancellationToken>()), Times.Once);
        _leaderboardStoreMock.Verify(x => x.DeleteAsync("ABC123", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class NotifyGameFinishedHandlerTests
{
    [Fact]
    public async Task Handle_ForwardsFinalLeaderboardToGameNotifier()
    {
        // Arrange
        var gameNotifierMock = new Mock<IGameNotifier>();
        var handler = new NotifyGameFinishedHandler(gameNotifierMock.Object);
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 150) };
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), leaderboard, new Dictionary<Guid, Guid?>());

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        gameNotifierMock.Verify(
            x => x.GameFinishedAsync("ABC123", leaderboard, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class SaveGameHistoryHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;

    public SaveGameHistoryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_SavesOneHistoryEntryPerLeaderboardRowWithCorrectPlacement()
    {
        // Arrange: leaderboard order defines placement (1st, 2nd, 3rd — 1-based, not 0-based)
        var quizSetId = Guid.CreateVersion7();
        var winnerUserId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry>
        {
            new(Guid.CreateVersion7(), "Winner", 200),
            new(Guid.CreateVersion7(), "RunnerUp", 100)
        };
        var participantUserIds = new Dictionary<Guid, Guid?>
        {
            [leaderboard[0].ParticipantId] = winnerUserId,
            [leaderboard[1].ParticipantId] = null // guest, no registered account
        };
        var handler = new SaveGameHistoryHandler(_dbContext);
        var notification = new GameFinishedNotification("ABC123", quizSetId, leaderboard, participantUserIds);

        // Act
        await handler.Handle(notification);

        // Assert
        var savedEntries = _dbContext.GameHistory.OrderBy(e => e.Placement).ToList();
        savedEntries.Should().HaveCount(2);

        savedEntries[0].DisplayName.Should().Be("Winner");
        savedEntries[0].Placement.Should().Be(1);
        savedEntries[0].ParticipantUserId.Should().Be(winnerUserId);
        savedEntries[0].FinalScore.Should().Be(200);

        savedEntries[1].DisplayName.Should().Be("RunnerUp");
        savedEntries[1].Placement.Should().Be(2);
        savedEntries[1].ParticipantUserId.Should().BeNull(); // guest player, correctly preserved as null
    }

    [Fact]
    public async Task Handle_WithEmptyLeaderboard_SavesNothingButStillSucceeds()
    {
        var handler = new SaveGameHistoryHandler(_dbContext);
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), [], new Dictionary<Guid, Guid?>());

        await handler.Handle(notification);

        _dbContext.GameHistory.Should().BeEmpty();
    }
}

public class SendGameResultsEmailHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<ILogger<SendGameResultsEmailHandler>> _loggerMock = new();

    private SendGameResultsEmailHandler CreateHandler()
        => new(_identityServiceMock.Object, _emailSenderMock.Object, _loggerMock.Object);

    [Fact]
    public async Task Handle_WhenNoParticipantHasARegisteredAccount_SendsNoEmailsAndSkipsIdentityLookup()
    {
        // Arrange: an all-guest game
        var handler = CreateHandler();
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Guest1", 50) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = null };
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), leaderboard, participantUserIds);

        // Act
        await handler.Handle(notification);

        // Assert
        _identityServiceMock.Verify(
            x => x.GetEmailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSenderMock.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SendsWinnerEmailWithTrophySubjectToFirstPlace()
    {
        // Arrange
        var winnerUserId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 300) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = winnerUserId };
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), leaderboard, participantUserIds);

        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(winnerUserId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [winnerUserId] = "ivan@test.com" });
        var handler = CreateHandler();

        // Act
        await handler.Handle(notification);

        // Assert: 1st place gets the "You won" subject line
        _emailSenderMock.Verify(
            x => x.SendAsync("ivan@test.com", It.Is<string>(s => s.Contains("won")), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SendsRunnerUpEmailWithPlacementSubject_NotWinnerSubject()
    {
        // Arrange
        var runnerUpUserId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry>
        {
            new(Guid.CreateVersion7(), "Winner", 300),
            new(Guid.CreateVersion7(), "SecondPlace", 200)
        };
        var participantUserIds = new Dictionary<Guid, Guid?>
        {
            [leaderboard[0].ParticipantId] = null, // guest winner — no email possible
            [leaderboard[1].ParticipantId] = runnerUpUserId
        };
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), leaderboard, participantUserIds);

        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [runnerUpUserId] = "second@test.com" });
        var handler = CreateHandler();

        // Act
        await handler.Handle(notification);

        // Assert: placement #2, not the winner subject
        _emailSenderMock.Verify(
            x => x.SendAsync(
                "second@test.com",
                It.Is<string>(s => s.Contains("#2") && !s.Contains("won")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSenderThrowsForOneRecipient_StillCompletesWithoutThrowing()
    {
        // Arrange: this mirrors SendWelcomeEmailHandler's defensive try/catch —
        // one failed email must not affect other recipients or bubble up.
        var userId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 100) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = userId };
        var notification = new GameFinishedNotification("ABC123", Guid.CreateVersion7(), leaderboard, participantUserIds);

        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId] = "ivan@test.com" });
        _emailSenderMock
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Resend API is down"));
        var handler = CreateHandler();

        // Act
        var act = async () => await handler.Handle(notification);

        // Assert
        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
