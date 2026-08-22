using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameHistory.Commands.SaveGameHistory;
using QuizArena.Application.Features.GameHistory.Events;
using QuizArena.Application.Features.GameRooms.Events;
using QuizArena.Domain.Entities;
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

// This handler used to not exist as production code at all — see the review response (W3/W6/W4/W7).
// GameHistoryEntry/GetMyGameHistory/the admin dashboard's "total games" stat all assumed *something* wrote
// to GameHistory, but nothing shipped ever did; only tests seeded it directly. SaveGameHistoryCommandHandler
// is the actual write path, and it's also where Player stats finally get updated (W6) and game-results
// emails get queued to the outbox (W4) — both of those naturally belong at "a game just produced a final
// leaderboard", which is exactly this moment.
public class SaveGameHistoryCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IOutboxWriter> _outboxWriterMock = new();

    public SaveGameHistoryCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private SaveGameHistoryCommandHandler CreateHandler()
        => new(_dbContext, _identityServiceMock.Object, _outboxWriterMock.Object);

    [Fact]
    public async Task Handle_SavesOneHistoryEntryPerLeaderboardRowWithCorrectGameIdAndPlacement()
    {
        // Arrange: leaderboard order defines placement (1st, 2nd, ... — 1-based, not 0-based)
        var gameId = Guid.CreateVersion7();
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
        var handler = CreateHandler();
        var command = new SaveGameHistoryCommand(gameId, quizSetId, leaderboard, participantUserIds);

        // Act
        var result = await handler.Handle(command);

        // Assert
        result.IsError.Should().BeFalse();

        var savedEntries = _dbContext.GameHistory.OrderBy(e => e.Placement).ToList();
        savedEntries.Should().HaveCount(2);

        savedEntries[0].GameId.Should().Be(gameId); // W7: shared by every row from this one game
        savedEntries[0].QuizSetId.Should().Be(quizSetId);
        savedEntries[0].DisplayName.Should().Be("Winner");
        savedEntries[0].Placement.Should().Be(1);
        savedEntries[0].ParticipantUserId.Should().Be(winnerUserId);
        savedEntries[0].FinalScore.Should().Be(200);

        savedEntries[1].GameId.Should().Be(gameId);
        savedEntries[1].DisplayName.Should().Be("RunnerUp");
        savedEntries[1].Placement.Should().Be(2);
        savedEntries[1].ParticipantUserId.Should().BeNull(); // guest player, correctly preserved as null
    }

    [Fact]
    public async Task Handle_WithEmptyLeaderboard_SavesNothingButStillSucceeds()
    {
        var handler = CreateHandler();
        var command = new SaveGameHistoryCommand(
            Guid.CreateVersion7(), Guid.CreateVersion7(), [], new Dictionary<Guid, Guid?>());

        var result = await handler.Handle(command);

        result.IsError.Should().BeFalse();
        _dbContext.GameHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForRegisteredParticipant_UpdatesPlayerTotalScoreAndGamesPlayed()
    {
        // W6: Player.RecordGameResult existed and was fully unit-tested, but nothing in production ever
        // called it — TotalGamesPlayed/TotalScore stayed 0 for every real player, forever.
        // Arrange
        var userId = Guid.CreateVersion7();
        var player = Player.Create(userId, "Ivan").Value;
        _dbContext.Players.Add(player);
        await _dbContext.SaveChangesAsync();

        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 150) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = userId };
        var handler = CreateHandler();

        // Act
        await handler.Handle(new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        // Assert
        var updatedPlayer = await _dbContext.Players.SingleAsync(p => p.Id == userId);
        updatedPlayer.TotalGamesPlayed.Should().Be(1);
        updatedPlayer.TotalScore.Should().Be(150);
    }

    [Fact]
    public async Task Handle_ForRegisteredParticipantWithZeroScore_StillCountsAsAGamePlayed()
    {
        // W6: 0 is a valid outcome (answered everything wrong / never in time) — only a negative score would
        // be invalid data. See PlayerTests for the domain-level version of this same fix.
        var userId = Guid.CreateVersion7();
        _dbContext.Players.Add(Player.Create(userId, "Ivan").Value);
        await _dbContext.SaveChangesAsync();

        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 0) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = userId };
        var handler = CreateHandler();

        await handler.Handle(new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        var updatedPlayer = await _dbContext.Players.SingleAsync(p => p.Id == userId);
        updatedPlayer.TotalGamesPlayed.Should().Be(1);
        updatedPlayer.TotalScore.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForGuestParticipant_DoesNotTouchAnyPlayerRow()
    {
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Guest1", 50) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = null };
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        result.IsError.Should().BeFalse();
        _dbContext.Players.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForRegisteredParticipantWithKnownEmail_EnqueuesGameResultsEmailOnOutbox()
    {
        // W4: this used to be a Task.WhenAll of live SMTP sends from inside SendGameResultsEmailHandler — a
        // notification handler completely decoupled from whether the game history it was reporting on ever
        // actually saved. Now it's an outbox row written in the *same* SaveChangesAsync as the GameHistory
        // rows and the Player update above, via IOutboxWriter — see OutboxWriter for how that becomes
        // transactional.
        var userId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Ivan", 300) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = userId };
        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(userId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId] = "ivan@test.com" });
        var handler = CreateHandler();

        await handler.Handle(new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        _outboxWriterMock.Verify(
            x => x.Enqueue(It.Is<GameResultsEmailMessage>(m =>
                m.Email == "ivan@test.com" && m.Score == 300 && m.Placement == 1 && m.IsWinner)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ForRunnerUp_EnqueuesEmailWithIsWinnerFalse()
    {
        var winnerUserId = Guid.CreateVersion7();
        var runnerUpUserId = Guid.CreateVersion7();
        var leaderboard = new List<LeaderboardEntry>
        {
            new(Guid.CreateVersion7(), "Winner", 300),
            new(Guid.CreateVersion7(), "RunnerUp", 200)
        };
        var participantUserIds = new Dictionary<Guid, Guid?>
        {
            [leaderboard[0].ParticipantId] = winnerUserId,
            [leaderboard[1].ParticipantId] = runnerUpUserId
        };
        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>
            {
                [winnerUserId] = "winner@test.com",
                [runnerUpUserId] = "runnerup@test.com"
            });
        var handler = CreateHandler();

        await handler.Handle(new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        _outboxWriterMock.Verify(
            x => x.Enqueue(It.Is<GameResultsEmailMessage>(m =>
                m.Email == "runnerup@test.com" && m.Placement == 2 && !m.IsWinner)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ForGuestParticipant_EnqueuesNoEmail()
    {
        // Arrange: an all-guest game
        var leaderboard = new List<LeaderboardEntry> { new(Guid.CreateVersion7(), "Guest1", 50) };
        var participantUserIds = new Dictionary<Guid, Guid?> { [leaderboard[0].ParticipantId] = null };
        var handler = CreateHandler();

        // Act
        await handler.Handle(new SaveGameHistoryCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), leaderboard, participantUserIds));

        // Assert
        _identityServiceMock.Verify(
            x => x.GetEmailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _outboxWriterMock.Verify(x => x.Enqueue(It.IsAny<GameResultsEmailMessage>()), Times.Never);
    }
}
