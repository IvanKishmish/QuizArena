using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.Auth.Events;

namespace QuizArena.Application.UnitTests.Features.Auth;

public class SendWelcomeEmailHandlerTests
{
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<ILogger<SendWelcomeEmailHandler>> _loggerMock = new();

    private SendWelcomeEmailHandler CreateHandler()
        => new(_emailSenderMock.Object, _loggerMock.Object);

    [Fact]
    public async Task Handle_WithValidNotification_SendsEmailToCorrectAddress()
    {
        // Arrange
        var handler = CreateHandler();
        var notification = new UserRegisteredNotification(Guid.CreateVersion7(), "ivan@test.com", "Ivan99");

        // Act
        await handler.Handle(notification);

        // Assert
        _emailSenderMock.Verify(
            x => x.SendAsync(
                "ivan@test.com",
                It.Is<string>(subject => subject.Contains("Welcome")),
                It.Is<string>(html => html.Contains("Ivan99")), // personalization in the email body
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSenderThrows_SwallowsExceptionAndLogsWarning()
    {
        // Arrange: the email provider (e.g. Resend API) is unavailable
        _emailSenderMock
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Resend API is down"));
        var handler = CreateHandler();
        var notification = new UserRegisteredNotification(Guid.CreateVersion7(), "ivan@test.com", "Ivan99");

        // Act
        var act = async () => await handler.Handle(notification);

        // Assert: the exception must NOT bubble up — registration shouldn't fail because of an email hiccup
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
