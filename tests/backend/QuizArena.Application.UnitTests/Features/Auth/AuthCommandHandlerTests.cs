using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.Auth.Events;
using QuizArena.Application.Features.Auth.Login;
using QuizArena.Application.Features.Auth.Logout;
using QuizArena.Application.Features.Auth.RefreshToken;
using QuizArena.Application.Features.Auth.Register;
using QuizArena.Persistence.Context;

namespace QuizArena.Application.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<LoginCommand>> _validatorMock = new();

    private LoginCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _tokenServiceMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutCallingIdentityService()
    {
        // Arrange
        var failure = new ValidationFailure("Email", "Email is required");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("", "password"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Email is required");
        _identityServiceMock.Verify(
            x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCredentialsAreInvalid_ReturnsErrorFromIdentityService()
    {
        // Arrange
        SetupValidatorSuccess();
        _identityServiceMock
            .Setup(x => x.ValidateCredentialsAsync("ivan@test.com", "wrong-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password."));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("ivan@test.com", "wrong-password"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTokenPairAndStoresHashedRefreshToken()
    {
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        _identityServiceMock
            .Setup(x => x.ValidateCredentialsAsync("ivan@test.com", "correct-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _identityServiceMock
            .Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["Player"]);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(userId, It.IsAny<IReadOnlyList<string>>())).Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new LoginCommand("ivan@test.com", "correct-password"));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("raw-refresh-token");

        // Important: the DATABASE stores a HASH of the refresh token, not the raw token itself.
        // TokenHasher.Hash("raw-refresh-token") is deterministic SHA256, so we can compare it directly.
        var expectedHash = QuizArena.Application.Common.TokenHasher.Hash("raw-refresh-token");
        _identityServiceMock.Verify(
            x => x.StoreRefreshTokenAsync(userId, expectedHash, TimeSpan.FromDays(7), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class RegisterCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IValidator<RegisterCommand>> _validatorMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();

    public RegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private RegisterCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _dbContext, _validatorMock.Object, _tokenServiceMock.Object, _publisherMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsErrorAndDoesNotCreatePlayer()
    {
        // Arrange
        SetupValidatorSuccess();
        _identityServiceMock
            .Setup(x => x.CreateUserAsync("taken@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Conflict("Auth.EmailAlreadyExists", "Email is already registered."));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RegisterCommand("Ivan99", "taken@test.com", "Password123"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.EmailAlreadyExists");
        _dbContext.Players.Should().BeEmpty();
        _publisherMock.Verify(x => x.Publish(It.IsAny<UserRegisteredNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesPlayerStoresTokenAndPublishesNotification()
    {
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        _identityServiceMock
            .Setup(x => x.CreateUserAsync("ivan@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _identityServiceMock
            .Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["Player"]);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(userId, It.IsAny<IReadOnlyList<string>>())).Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RegisterCommand("Ivan99", "ivan@test.com", "Password123"));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");

        var savedPlayer = await _dbContext.Players.FindAsync(userId);
        savedPlayer.Should().NotBeNull();
        savedPlayer!.NickName.Should().Be("Ivan99");

        _publisherMock.Verify(
            x => x.Publish(
                It.Is<UserRegisteredNotification>(n => n.UserId == userId && n.Email == "ivan@test.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class LogoutCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();

    private LogoutCommandHandler CreateHandler() => new(_identityServiceMock.Object);

    [Fact]
    public async Task Handle_WithEmptyRefreshToken_ReturnsSuccessWithoutCallingIdentityService()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LogoutCommand(""));

        result.IsError.Should().BeFalse();
        _identityServiceMock.Verify(
            x => x.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonEmptyRefreshToken_RevokesHashedToken()
    {
        var handler = CreateHandler();
        var expectedHash = QuizArena.Application.Common.TokenHasher.Hash("some-refresh-token");

        var result = await handler.Handle(new LogoutCommand("some-refresh-token"));

        result.IsError.Should().BeFalse();
        _identityServiceMock.Verify(
            x => x.RevokeRefreshTokenAsync(expectedHash, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IValidator<RefreshTokenCommand>> _validatorMock = new();

    private RefreshTokenCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _tokenServiceMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenTokenIsInvalidOrExpired_ReturnsNotFoundError()
    {
        SetupValidatorSuccess();
        _identityServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new RefreshTokenCommand("expired-token"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
        _identityServiceMock.Verify(
            x => x.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidToken_RevokesOldTokenAndIssuesNewTokenPair()
    {
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        var oldTokenHash = QuizArena.Application.Common.TokenHasher.Hash("old-refresh-token");
        _identityServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(oldTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _identityServiceMock
            .Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["Player"]);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(userId, It.IsAny<IReadOnlyList<string>>())).Returns("new-access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RefreshTokenCommand("old-refresh-token"));

        // Assert — old token is revoked (rotation), a new one is issued
        result.IsError.Should().BeFalse();
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        _identityServiceMock.Verify(x => x.RevokeRefreshTokenAsync(oldTokenHash, It.IsAny<CancellationToken>()), Times.Once);
        _identityServiceMock.Verify(
            x => x.StoreRefreshTokenAsync(userId, It.IsAny<string>(), TimeSpan.FromDays(7), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
