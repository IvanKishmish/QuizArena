using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;
using QuizArena.Persistence.Context;

namespace QuizArena.Application.UnitTests.Features.QuizSets.Queries;

public class GetQuizSetByIdQueryHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
 
    public GetQuizSetByIdQueryHandlerTests()
    {
        // Each test gets its own isolated "database" via a unique name —
        // tests do not see each other's data and can run concurrently.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
 
        _dbContext = new AppDbContext(options);
    }
 
    public void Dispose() => _dbContext.Dispose();
 
    private GetQuizSetByIdQueryHandler CreateHandler()
        => new(_dbContext, _currentUserMock.Object);
 
    private async Task<QuizSet> SeedQuizSet(Guid ownerId, Visibility visibility)
    {
        var quizSet = QuizSet.Create(new QuizSetCreationParams(ownerId, "Title", "Description")).Value;
 
        if (visibility == Visibility.Public)
            quizSet.Publish();
 
        _dbContext.QuizSets.Add(quizSet);
        await _dbContext.SaveChangesAsync();
 
        return quizSet;
    }
 
    [Fact]
    public async Task Handle_WhenQuizSetDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        var handler = CreateHandler();
        var query = new GetQuizSetByIdQuery(Guid.CreateVersion7());
 
        // Act
        var result = await handler.Handle(query);
 
        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }
 
    [Fact]
    public async Task Handle_WhenQuizSetIsPublic_ReturnsItToAnyUser()
    {
        // Arrange
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Public);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7()); 
 
        var handler = CreateHandler();
        var query = new GetQuizSetByIdQuery(quizSet.Id);
 
        // Act
        var result = await handler.Handle(query);
 
        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(quizSet.Id);
    }
 
    [Fact]
    public async Task Handle_WhenQuizSetIsPrivateAndRequestedByOwner_ReturnsIt()
    {
        // Arrange
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Private);
        _currentUserMock.Setup(x => x.UserId).Returns(ownerId); // the owner
 
        var handler = CreateHandler();
        var query = new GetQuizSetByIdQuery(quizSet.Id);
 
        // Act
        var result = await handler.Handle(query);
 
        // Assert
        result.IsError.Should().BeFalse();
        result.Value.OwnerId.Should().Be(ownerId);
    }
 
    [Fact]
    public async Task Handle_WhenQuizSetIsPrivateAndRequestedByStranger_ReturnsNotFoundError()
    {
        // Arrange
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Private);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7()); //not owner
 
        var handler = CreateHandler();
        var query = new GetQuizSetByIdQuery(quizSet.Id);
 
        // Act
        var result = await handler.Handle(query);
 
        // Assert — intentionally NotFound rather than Forbidden: a private quiz 
        // should not even leak the fact of its existence to unauthorized users.
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }
 
    [Fact]
    public async Task Handle_WhenQuizSetIsPrivateAndUserIsAnonymous_ReturnsNotFoundError()
    {
        // Arrange
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Private);
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null); // Unauthorized
 
        var handler = CreateHandler();
        var query = new GetQuizSetByIdQuery(quizSet.Id);
 
        // Act
        var result = await handler.Handle(query);
 
        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }
}