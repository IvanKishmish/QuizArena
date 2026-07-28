using FluentAssertions;
using Moq;
using QuizArena.Application.Common.Interfaces.IdentityQueryService;
using QuizArena.Application.Features.Admin.Queries.GetAllQuizSetsForModeration;
using QuizArena.Application.Features.Admin.Queries.GetAllUsers;
using QuizArena.Application.Features.Admin.Queries.GetDashboardStats;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.Admin;

public class GetAllQuizSetsForModerationQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetAllQuizSetsForModerationQueryHandler CreateHandler() => new(DbContext);

    [Fact]
    public async Task Handle_ReturnsBothPrivateAndPublicQuizSets()
    {
        // Arrange: unlike GetPublicQuizSets, moderation must see everything, including private ones
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, "Public quiz");
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Private, "Private quiz");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetAllQuizSetsForModerationQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_RespectsPagination()
    {
        // Arrange
        for (var i = 1; i <= 3; i++)
        {
            await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, $"Quiz {i}");
            await Task.Delay(5);
        }
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetAllQuizSetsForModerationQuery(PageNumber: 1, PageSize: 2));

        // Assert
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
    }
}

public class GetAllUsersQueryHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IIdentityUserQueryService> _identityUserQueryServiceMock = new();

    private GetAllUsersQueryHandler CreateHandler()
        => new(_identityUserQueryServiceMock.Object, DbContext);

    [Fact]
    public async Task Handle_MergesIdentityDataWithNicknamesFromPlayersTable()
    {
        // Arrange: user data lives in ASP.NET Identity, nicknames live in our own Players table —
        // the handler's whole job is joining the two by Id.
        var userId = Guid.CreateVersion7();
        var identityUsers = new List<IdentityUserSummary>
        {
            new(userId, "ivan@test.com", IsBanned: false, RegisteredAt: DateTimeOffset.UtcNow)
        };
        _identityUserQueryServiceMock
            .Setup(x => x.GetPagedUsersAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((identityUsers, 1));

        DbContext.Players.Add(Player.Create(userId, "Ivan99").Value);
        await DbContext.SaveChangesAsync();
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetAllUsersQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(u => u.Id == userId && u.Email == "ivan@test.com" && u.Nickname == "Ivan99");
    }

    [Fact]
    public async Task Handle_WhenPlayerRecordIsMissing_FallsBackToPlaceholderNickname()
    {
        // Arrange: identity user exists, but the corresponding Player row is missing/not yet created
        var userId = Guid.CreateVersion7();
        var identityUsers = new List<IdentityUserSummary>
        {
            new(userId, "orphan@test.com", IsBanned: false, RegisteredAt: DateTimeOffset.UtcNow)
        };
        _identityUserQueryServiceMock
            .Setup(x => x.GetPagedUsersAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((identityUsers, 1));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetAllUsersQuery());

        // Assert
        result.Value.Items.Single().Nickname.Should().Be("—");
    }
}

public class GetDashboardStatsQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetDashboardStatsQueryHandler CreateHandler() => new(DbContext);

    [Fact]
    public async Task Handle_AggregatesCountsAcrossPlayersQuizSetsAndGameHistory()
    {
        // Arrange
        DbContext.Players.Add(Player.Create(Guid.CreateVersion7(), "Ivan1").Value);
        DbContext.Players.Add(Player.Create(Guid.CreateVersion7(), "Olena").Value);
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Private);
        var historyEntry = GameHistoryEntry.Create(new GameHistoryEntryCreationParams(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Ivan", 100, 1)).Value;
        DbContext.GameHistory.Add(historyEntry);
        await DbContext.SaveChangesAsync();
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetDashboardStatsQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.TotalUsers.Should().Be(2);
        result.Value.TotalQuizSets.Should().Be(2);
        result.Value.TotalPublishedQuizSets.Should().Be(1); // only the public one
        result.Value.TotalGamesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoDataAtAll_ReturnsAllZeroes()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStatsQuery());

        result.Value.Should().Be(new DashboardStats(0, 0, 0, 0));
    }
}
