using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Features.QuizSets.Queries.GetMyQuizSets;
using QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.QuizSets.Queries;

public class GetMyQuizSetsQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetMyQuizSetsQueryHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object);

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyQuizSetsQuery());

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
    }

    [Fact]
    public async Task Handle_ReturnsOnlyQuizSetsOwnedByCurrentUser()
    {
        // Arrange
        var myId = Guid.CreateVersion7();
        var strangerId = Guid.CreateVersion7();
        await SeedQuizSet(myId, title: "My quiz 1");
        await SeedQuizSet(myId, title: "My quiz 2");
        await SeedQuizSet(strangerId, title: "Not mine");

        CurrentUserMock.Setup(x => x.UserId).Returns(myId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetMyQuizSetsQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(qs => qs.Title.StartsWith("My quiz"));
    }

    [Fact]
    public async Task Handle_WhenUserHasNoQuizSets_ReturnsEmptyList()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new GetMyQuizSetsQuery());

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}

public class GetPublicQuizSetsQueryHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IValidator<GetPublicQuizSetsQuery>> _validatorMock = new();

    private GetPublicQuizSetsQueryHandler CreateHandler() => new(DbContext, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetPublicQuizSetsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_ReturnsOnlyPublicQuizSets()
    {
        SetupValidatorSuccess();
        // Arrange
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, "Public 1");
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Private, "Private 1");
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, "Public 2");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPublicQuizSetsQuery());

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(qs => qs.Title.StartsWith("Public"));
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OrdersResultsByCreatedAtDescending()
    {
        SetupValidatorSuccess();
        // Arrange: save one at a time, with a small delay — so that CreatedAt
        // (set by the interceptor on SaveChanges) is guaranteed to differ.
        var first = await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, "First created");
        await Task.Delay(10);
        var second = await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, "Second created");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetPublicQuizSetsQuery());

        // Assert: the most recently created one (Second) should come first
        result.Value.Items.Select(qs => qs.Id).Should().ContainInOrder(second.Id, first.Id);
    }

    [Fact]
    public async Task Handle_RespectsPageSizeAndPageNumber()
    {
        SetupValidatorSuccess();
        // Arrange: 5 public quiz sets
        for (var i = 1; i <= 5; i++)
        {
            await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public, $"Quiz {i}");
            await Task.Delay(5);
        }
        var handler = CreateHandler();

        // Act: second page, 2 items per page
        var result = await handler.Handle(new GetPublicQuizSetsQuery(PageNumber: 2, PageSize: 2));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalPages.Should().Be(3); // ceil(5 / 2)
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutQueryingDatabase()
    {
        // W8: an oversized PageSize used to reach Skip/Take completely unchecked.
        var failure = new ValidationFailure("PageSize", "PageSize must be between 1 and 100.");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<GetPublicQuizSetsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetPublicQuizSetsQuery(PageNumber: 1, PageSize: 1_000_000));

        result.IsError.Should().BeTrue();
    }
}
