using ErrorOr;
using FluentAssertions;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.Admin.Commands.BanUser;
using QuizArena.Application.Features.Admin.Commands.DeleteAnyQuizSet;
using QuizArena.Application.Features.Admin.Commands.UnbanUser;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.Admin;

public class BanUserCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private BanUserCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _currentUserMock.Object);

    [Fact]
    public async Task Handle_WhenAdminTriesToBanThemselves_ReturnsValidationErrorWithoutCallingIdentityService()
    {
        // Arrange: a safety guard against an admin locking themselves out
        var adminId = Guid.CreateVersion7();
        _currentUserMock.Setup(x => x.UserId).Returns(adminId);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new BanUserCommand(adminId));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CantBanYourself");
        _identityServiceMock.Verify(
            x => x.BanUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBanningAnotherUser_DelegatesToIdentityServiceAndReturnsItsResult()
    {
        // Arrange
        var adminId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();
        _currentUserMock.Setup(x => x.UserId).Returns(adminId);
        _identityServiceMock
            .Setup(x => x.BanUserAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Updated);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new BanUserCommand(targetUserId));

        // Assert
        result.IsError.Should().BeFalse();
        _identityServiceMock.Verify(x => x.BanUserAsync(targetUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenIdentityServiceReturnsNotFound_PropagatesTheErrorAsIs()
    {
        // Arrange: the handler is a thin pass-through — no swallowing/rewriting of errors
        var adminId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();
        _currentUserMock.Setup(x => x.UserId).Returns(adminId);
        _identityServiceMock
            .Setup(x => x.BanUserAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.NotFound("User.NotFound", "User not found."));
        var handler = CreateHandler();

        var result = await handler.Handle(new BanUserCommand(targetUserId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.NotFound");
    }
}

public class UnbanUserCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();

    [Fact]
    public async Task Handle_DelegatesToIdentityServiceAndReturnsItsResult()
    {
        // Arrange
        var handler = new UnbanUserCommandHandler(_identityServiceMock.Object);
        var targetUserId = Guid.CreateVersion7();
        _identityServiceMock
            .Setup(x => x.UnbanUserAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Updated);

        // Act
        var result = await handler.Handle(new UnbanUserCommand(targetUserId));

        // Assert
        result.IsError.Should().BeFalse();
        _identityServiceMock.Verify(x => x.UnbanUserAsync(targetUserId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class DeleteAnyQuizSetCommandHandlerTests : QuizArenaHandlerTestBase
{
    private DeleteAnyQuizSetCommandHandler CreateHandler()
        => new(DbContext, QuestionStoreMock.Object);

    [Fact]
    public async Task Handle_WhenQuizSetDoesNotExist_ReturnsNotFoundError()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteAnyQuizSetCommand(Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }

    [Fact]
    public async Task Handle_WithExistingQuizSet_DeletesItRegardlessOfOwnerOrVisibility()
    {
        // Arrange: unlike the regular DeleteQuizSet handler, this one has NO ownership
        // or "must be private" check — admins can remove any quiz set, published or not.
        var quizSet = await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DeleteAnyQuizSetCommand(quizSet.Id));

        // Assert
        result.IsError.Should().BeFalse();
        (await DbContext.QuizSets.FindAsync(quizSet.Id)).Should().BeNull();
        QuestionStoreMock.Verify(
            x => x.DeleteByQuizSetIdAsync(quizSet.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
