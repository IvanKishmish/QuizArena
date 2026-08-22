using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
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
    private readonly Mock<IValidator<GetAllQuizSetsForModerationQuery>> _validatorMock = new();

    private GetAllQuizSetsForModerationQueryHandler CreateHandler() => new(DbContext, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetAllQuizSetsForModerationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_ReturnsBothPrivateAndPublicQuizSets()
    {
        // Arrange: unlike GetPublicQuizSets, moderation must see everything, including private ones
        SetupValidatorSuccess();
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
        SetupValidatorSuccess();
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

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutQueryingDatabase()
    {
        // W8: PageNumber/PageSize used to reach Skip/Take completely unchecked (?pageNumber=0 threw,
        // ?pageSize=1000000 dumped the whole table). Now a validator runs first, same as every other handler.
        var failure = new ValidationFailure("PageSize", "PageSize must be between 1 and 100.");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetAllQuizSetsForModerationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAllQuizSetsForModerationQuery(PageNumber: 1, PageSize: 100_000));

        result.IsError.Should().BeTrue();
    }
}

public class GetAllUsersQueryHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IIdentityUserQueryService> _identityUserQueryServiceMock = new();
    private readonly Mock<IValidator<GetAllUsersQuery>> _validatorMock = new();

    private GetAllUsersQueryHandler CreateHandler()
        => new(_identityUserQueryServiceMock.Object, DbContext, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_MergesIdentityDataWithNicknamesFromPlayersTable()
    {
        // Arrange: user data lives in ASP.NET Identity, nicknames live in our own Players table —
        // the handler's whole job is joining the two by Id.
        SetupValidatorSuccess();
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
        SetupValidatorSuccess();
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

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutCallingIdentityQueryService()
    {
        var failure = new ValidationFailure("PageNumber", "PageNumber must be at least 1.");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAllUsersQuery(PageNumber: 0));

        result.IsError.Should().BeTrue();
        _identityUserQueryServiceMock.Verify(
            x => x.GetPagedUsersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class GetDashboardStatsQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetDashboardStatsQueryHandler CreateHandler() => new(DbContext);

    private static GameHistoryEntry CreateHistoryEntry(Guid gameId, string displayName = "Ivan", int score = 100, int placement = 1) =>
        GameHistoryEntry.Create(new GameHistoryEntryCreationParams(
            gameId, Guid.CreateVersion7(), Guid.CreateVersion7(), displayName, score, placement)).Value;

    [Fact]
    public async Task Handle_AggregatesCountsAcrossPlayersQuizSetsAndGameHistory()
    {
        // Arrange
        DbContext.Players.Add(Player.Create(Guid.CreateVersion7(), "Ivan1").Value);
        DbContext.Players.Add(Player.Create(Guid.CreateVersion7(), "Olena").Value);
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Private);
        DbContext.GameHistory.Add(CreateHistoryEntry(Guid.CreateVersion7()));
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
    public async Task Handle_WithMultipleParticipantsInOneGame_CountsItAsExactlyOneGame()
    {
        // W7: was GameHistory.Select(g => g.Id).Distinct().Count() — g.Id is each row's own primary key, so
        // Distinct() never removed anything and a 10-player game counted as 10 "games". These three rows
        // share one GameId (one real game, three participants) and must count as 1, not 3.
        var gameId = Guid.CreateVersion7();
        DbContext.GameHistory.Add(CreateHistoryEntry(gameId, "Winner", 300, 1));
        DbContext.GameHistory.Add(CreateHistoryEntry(gameId, "RunnerUp", 200, 2));
        DbContext.GameHistory.Add(CreateHistoryEntry(gameId, "ThirdPlace", 100, 3));
        await DbContext.SaveChangesAsync();
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStatsQuery());

        result.Value.TotalGamesPlayed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithTwoSeparateGames_CountsTwo()
    {
        DbContext.GameHistory.Add(CreateHistoryEntry(Guid.CreateVersion7()));
        DbContext.GameHistory.Add(CreateHistoryEntry(Guid.CreateVersion7()));
        await DbContext.SaveChangesAsync();
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStatsQuery());

        result.Value.TotalGamesPlayed.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithNoDataAtAll_ReturnsAllZeroes()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStatsQuery());

        result.Value.Should().Be(new DashboardStats(0, 0, 0, 0));
    }
}
