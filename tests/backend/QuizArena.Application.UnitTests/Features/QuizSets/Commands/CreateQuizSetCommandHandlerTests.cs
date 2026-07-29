using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.UnitTests.Features.QuizSets.Commands;

public sealed class CreateQuizSetCommandHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IValidator<CreateQuizSetCommand>> _validatorMock = new();
    private readonly Mock<DbSet<QuizSet>> _quizSetsDbSetMock = new();
 
    private CreateQuizSetCommandHandler CreateHandler()
    {
        _dbContextMock.Setup(x => x.QuizSets).Returns(_quizSetsDbSetMock.Object);
 
        return new CreateQuizSetCommandHandler(
            _dbContextMock.Object,
            _currentUserMock.Object,
            _validatorMock.Object);
    }
 
    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();
        var command = new CreateQuizSetCommand("Title", "Description");
 
        // Act
        var result = await handler.Handle(command);
 
        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
 
        // Ensure that validator and database calls were never reached
        _validatorMock.Verify(x => x.ValidateAsync(It.IsAny<CreateQuizSetCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
 
    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrors()
    {
        // Arrange
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
 
        var validationFailure = new ValidationFailure("Title", "Title must not be empty");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<CreateQuizSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([validationFailure]));
 
        var handler = CreateHandler();
        var command = new CreateQuizSetCommand("", "Description");
 
        // Act
        var result = await handler.Handle(command);
 
        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Title must not be empty");
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
 
    [Fact]
    public async Task Handle_WithValidCommand_AddsQuizSetAndSavesChanges()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
 
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<CreateQuizSetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult()); // no errors
 
        _dbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
 
        var handler = CreateHandler();
        var command = new CreateQuizSetCommand("My Quiz", "Description");
 
        // Act
        var result = await handler.Handle(command);
 
        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty(); // Returns Id of the newly created QuizSet
 
        _quizSetsDbSetMock.Verify(
            x => x.Add(It.Is<QuizSet>(qs => qs.OwnerId == userId && qs.Title == "My Quiz")),
            Times.Once);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}