using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Features.Questions.Commands.AddQuestion;
using QuizArena.Application.Features.Questions.Commands.DeleteQuestion;
using QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.Questions;

public class AddQuestionCommandHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IValidator<AddQuestionCommand>> _validatorMock = new();

    private AddQuestionCommandHandler CreateHandler()
        => new(DbContext, QuestionStoreMock.Object, CurrentUserMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<AddQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private static AddQuestionCommand CreateCommand(Guid quizSetId) => new(
        quizSetId,
        Text: "2 + 2 = ?",
        QuestionType: QuestionType.SingleChoice,
        TimeLimitSeconds: 30,
        Points: 100,
        Options:
        [
            new AnswerOptionDto("4", true, 0),
            new AnswerOptionDto("5", false, 1)
        ]);

    [Fact]
    public async Task Handle_WhenQuizSetDoesNotExist_ReturnsNotFoundError()
    {
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }

    [Fact]
    public async Task Handle_WhenQuestionDomainRuleIsViolated_ReturnsValidationError()
    {
        // Two correct answers for SingleChoice — violates the Question domain invariant
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var invalidCommand = new AddQuestionCommand(
            quizSet.Id, "Text", QuestionType.SingleChoice, 30, 100,
            [new AnswerOptionDto("A", true, 0), new AnswerOptionDto("B", true, 1)]);

        var result = await handler.Handle(invalidCommand);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.SingleCorrectAnswerRequired");
        QuestionStoreMock.Verify(
            x => x.InsertAsync(It.IsAny<Guid>(), It.IsAny<QuizArena.Domain.Entities.Question>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCommand_InsertsQuestionIntoStoreAndReturnsId()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        SetupValidatorSuccess();
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(quizSet.Id));

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        QuestionStoreMock.Verify(
            x => x.InsertAsync(quizSet.Id, It.Is<QuizArena.Domain.Entities.Question>(q => q.Text == "2 + 2 = ?"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class DeleteQuestionCommandHandlerTests : QuizArenaHandlerTestBase
{
    private DeleteQuestionCommandHandler CreateHandler()
        => new(DbContext, QuestionStoreMock.Object, CurrentUserMock.Object);

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuestionCommand(quizSet.Id, Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }

    [Fact]
    public async Task Handle_WhenQuestionDoesNotExistInStore_ReturnsNotFoundError()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        QuestionStoreMock
            .Setup(x => x.GetByIdAsync(quizSet.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuizArena.Domain.Entities.Question?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuestionCommand(quizSet.Id, Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.NotFound");
    }

    [Fact]
    public async Task Handle_WhenQuestionExists_DeletesItFromStore()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        var question = CreateValidQuestion();
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        QuestionStoreMock
            .Setup(x => x.GetByIdAsync(quizSet.Id, question.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);
        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteQuestionCommand(quizSet.Id, question.Id));

        result.IsError.Should().BeFalse();
        QuestionStoreMock.Verify(
            x => x.DeleteAsync(quizSet.Id, question.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetQuestionsByQuizSetQueryHandlerTests : QuizArenaHandlerTestBase
{
    private GetQuestionsByQuizSetQueryHandler CreateHandler()
        => new(DbContext, QuestionStoreMock.Object, CurrentUserMock.Object);

    [Fact]
    public async Task Handle_WhenUserIsNotOwner_ReturnsForbiddenError()
    {
        var quizSet = await SeedQuizSet(Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        var result = await handler.Handle(new GetQuestionsByQuizSetQuery(quizSet.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
    }

    [Fact]
    public async Task Handle_WithOwnedQuizSet_ReturnsQuestionsFromStore()
    {
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        var question = CreateValidQuestion("What is DDD?");
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        QuestionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(quizSet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([question]);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetQuestionsByQuizSetQuery(quizSet.Id));

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(q => q.Text == "What is DDD?");
    }
}
