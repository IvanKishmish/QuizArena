using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Features.QuizSets.Commands.DeleteQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.PublishQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.UnpublishQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.QuizSets.Commands;

public class DeleteQuizSetCommandHandlerTests : QuizArenaHandlerTestBase
{
    private DeleteQuizSetCommandHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object, QuestionStoreMock.Object);

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuizSetCommand(Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
    }

    [Fact]
    public async Task Handle_WhenQuizSetDoesNotExist_ReturnsNotFoundError()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuizSetCommand(Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(ownerId: Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7()); // someone else
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }

    [Fact]
    public async Task Handle_WhenQuizSetIsPublic_ReturnsValidationError()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Public);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotAvailableToDelete");
    }

    [Fact]
    public async Task Handle_WithValidPrivateOwnedQuizSet_DeletesQuizSetAndQuestions()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Private);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeFalse();
        (await DbContext.QuizSets.FindAsync(quizSet.Id)).Should().BeNull();
        QuestionStoreMock.Verify(x => x.DeleteByQuizSetIdAsync(quizSet.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class PublishQuizSetCommandHandlerTests : QuizArenaHandlerTestBase
{
    private PublishQuizSetCommandHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object, QuestionStoreMock.Object);

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(ownerId: Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new PublishQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }

    [Fact]
    public async Task Handle_WhenQuizSetHasNoQuestions_ReturnsValidationError()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        QuestionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(quizSet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // empty question list
        var handler = CreateHandler();

        var result = await handler.Handle(new PublishQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.CannotPublishEmpty");
    }

    [Fact]
    public async Task Handle_WhenQuizSetHasQuestions_PublishesQuizSet()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        QuestionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(quizSet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateValidQuestion()]);
        var handler = CreateHandler();

        var result = await handler.Handle(new PublishQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeFalse();
        var updated = await DbContext.QuizSets.FindAsync(quizSet.Id);
        updated!.Visibility.Should().Be(Visibility.Public);
    }
}

public class UnpublishQuizSetCommandHandlerTests : QuizArenaHandlerTestBase
{
    private UnpublishQuizSetCommandHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object);

    [Fact]
    public async Task Handle_WithValidOwnedQuizSet_UnpublishesQuizSet()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, Visibility.Public);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        var handler = CreateHandler();

        var result = await handler.Handle(new UnpublishQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeFalse();
        var updated = await DbContext.QuizSets.FindAsync(quizSet.Id);
        updated!.Visibility.Should().Be(Visibility.Private);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(Guid.CreateVersion7(), Visibility.Public);
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new UnpublishQuizSetCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }
}

public class UpdateQuizSetDetailsCommandHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IValidator<UpdateQuizSetDetailsCommand>> _validatorMock = new();

    private UpdateQuizSetDetailsCommandHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<UpdateQuizSetDetailsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorsWithoutTouchingDb()
    {
        var failure = new ValidationFailure("Title", "Title is too long");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<UpdateQuizSetDetailsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new UpdateQuizSetDetailsCommand(Guid.CreateVersion7(), "", "New description"));

        result.IsError.Should().BeTrue();
        result.FirstError.Description.Should().Be("Title is too long");
    }

    [Fact]
    public async Task Handle_WithValidCommandAndOwnedQuizSet_UpdatesTitleAndDescription()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId, title: "Old title", description: "Old description");
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateQuizSetDetailsCommand(quizSet.Id, "New title", "New description"));

        result.IsError.Should().BeFalse();
        var updated = await DbContext.QuizSets.FindAsync(quizSet.Id);
        updated!.Title.Should().Be("New title");
        updated.Description.Should().Be("New description");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateQuizSetDetailsCommand(quizSet.Id, "New title", "New description"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }
}
