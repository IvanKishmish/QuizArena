using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Options;
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
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpiryDays = 7 });

    private LoginCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _tokenServiceMock.Object, _jwtOptions, _validatorMock.Object);

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
        _tokenServiceMock.Verify(
            x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTokenPairAndStoresHashedRefreshTokenWithFamilyId()
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
        // S7: the access token now also carries the user's email, not just sub/roles.
        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(userId, "ivan@test.com", It.IsAny<IReadOnlyList<string>>()))
            .Returns("access-token");
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

        // S6: a fresh login always starts a brand-new token family (some non-empty Guid) — the exact value
        // is unpredictable (Guid.CreateVersion7()), so we only assert it's set, not what it equals.
        _identityServiceMock.Verify(
            x => x.StoreRefreshTokenAsync(
                userId, It.Is<Guid>(f => f != Guid.Empty), expectedHash, TimeSpan.FromDays(7), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class RegisterCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IValidator<RegisterCommand>> _validatorMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IOutboxWriter> _outboxWriterMock = new();
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpiryDays = 7 });

    public RegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
    }

    public void Dispose() => _dbContext.Dispose();

    private RegisterCommandHandler CreateHandler() => new(
        _identityServiceMock.Object, _dbContext, _outboxWriterMock.Object, _validatorMock.Object,
        _tokenServiceMock.Object, _jwtOptions);

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
        _outboxWriterMock.Verify(x => x.Enqueue(It.IsAny<WelcomeEmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesPlayerStoresTokenAndEnqueuesWelcomeEmail()
    {
        // W4: replaces the old `_publisherMock.Verify(x => x.Publish(It.IsAny<UserRegisteredNotification>()...`
        // assertion — the welcome email is now an outbox row (IOutboxWriter.Enqueue), written in the same
        // SaveChangesAsync as the Player row, instead of a MediatR notification that called SMTP directly.
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        _identityServiceMock
            .Setup(x => x.CreateUserAsync("ivan@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        _identityServiceMock
            .Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["Player"]);
        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(userId, "ivan@test.com", It.IsAny<IReadOnlyList<string>>()))
            .Returns("access-token");
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

        _outboxWriterMock.Verify(
            x => x.Enqueue(It.Is<WelcomeEmailMessage>(m =>
                m.UserId == userId && m.Email == "ivan@test.com" && m.NickName == "Ivan99")),
            Times.Once);

        _identityServiceMock.Verify(
            x => x.StoreRefreshTokenAsync(
                userId, It.Is<Guid>(f => f != Guid.Empty), It.IsAny<string>(), TimeSpan.FromDays(7), It.IsAny<CancellationToken>()),
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
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpiryDays = 7 });

    private RefreshTokenCommandHandler CreateHandler()
        => new(_identityServiceMock.Object, _tokenServiceMock.Object, _jwtOptions, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenTokenIsUnknown_ReturnsInvalidRefreshTokenError()
    {
        SetupValidatorSuccess();
        _identityServiceMock
            .Setup(x => x.FindRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenLookup?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new RefreshTokenCommand("never-issued-token"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
        _identityServiceMock.Verify(
            x => x.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenHasExpired_ReturnsInvalidRefreshTokenError()
    {
        // Arrange: expired is a normal, non-suspicious outcome — must NOT trigger family revocation.
        SetupValidatorSuccess();
        var lookup = new RefreshTokenLookup(Guid.CreateVersion7(), Guid.CreateVersion7(), RefreshTokenStatus.Expired);
        _identityServiceMock
            .Setup(x => x.FindRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);
        var handler = CreateHandler();

        var result = await handler.Handle(new RefreshTokenCommand("expired-token"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
        _identityServiceMock.Verify(
            x => x.RevokeRefreshTokenFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenWasAlreadyUsed_DetectsReuseAndRevokesTheWholeFamily()
    {
        // S6: this is the actual theft-detection behavior under test — a *revoked* (not expired) token being
        // presented again means it was already rotated away once, so every token in its family gets killed.
        // Arrange
        SetupValidatorSuccess();
        var familyId = Guid.CreateVersion7();
        var lookup = new RefreshTokenLookup(Guid.CreateVersion7(), familyId, RefreshTokenStatus.Revoked);
        _identityServiceMock
            .Setup(x => x.FindRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RefreshTokenCommand("already-rotated-token"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.RefreshTokenReuseDetected");
        _identityServiceMock.Verify(x => x.RevokeRefreshTokenFamilyAsync(familyId, It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(
            x => x.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsLockedOut_RevokesFamilyAndReturnsForbidden()
    {
        // W2: catches the "temporarily locked out, but tokens are still technically Active" gap — a banned
        // user's tokens should already be Revoked (via IdentityService.BanUserAsync), so this specifically
        // covers a lockout that isn't a full ban yet.
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();
        var lookup = new RefreshTokenLookup(userId, familyId, RefreshTokenStatus.Active);
        _identityServiceMock
            .Setup(x => x.FindRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);
        _identityServiceMock.Setup(x => x.IsUserLockedOutAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RefreshTokenCommand("token-for-locked-out-user"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.UserLockedOut");
        _identityServiceMock.Verify(x => x.RevokeRefreshTokenFamilyAsync(familyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithActiveToken_RevokesOldTokenAndIssuesNewTokenPairInTheSameFamily()
    {
        // Arrange
        SetupValidatorSuccess();
        var userId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();
        var oldTokenHash = QuizArena.Application.Common.TokenHasher.Hash("old-refresh-token");
        var lookup = new RefreshTokenLookup(userId, familyId, RefreshTokenStatus.Active);
        _identityServiceMock
            .Setup(x => x.FindRefreshTokenAsync(oldTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup);
        _identityServiceMock.Setup(x => x.IsUserLockedOutAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _identityServiceMock
            .Setup(x => x.GetUserRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["Player"]);
        _identityServiceMock
            .Setup(x => x.GetEmailsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(userId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId] = "ivan@test.com" });
        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(userId, "ivan@test.com", It.IsAny<IReadOnlyList<string>>()))
            .Returns("new-access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new RefreshTokenCommand("old-refresh-token"));

        // Assert — old token is revoked (rotation), a new one is issued in the SAME family (S6)
        result.IsError.Should().BeFalse();
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        _identityServiceMock.Verify(x => x.RevokeRefreshTokenAsync(oldTokenHash, It.IsAny<CancellationToken>()), Times.Once);
        _identityServiceMock.Verify(
            x => x.StoreRefreshTokenAsync(userId, familyId, It.IsAny<string>(), TimeSpan.FromDays(7), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
