    using ErrorOr;
    using QuizArena.Domain.Common;
    using QuizArena.Domain.Entities.Models;
    using QuizArena.Domain.Enums;

    namespace QuizArena.Domain.Entities;

    public sealed class QuizSet : Entity
    {
        public Guid OwnerId { get; private init;}
        
        public string Title { get; private set; } = string.Empty;
        
        public string Description { get; private set; } = string.Empty;

        public Visibility Visibility { get; private set; } = Visibility.Private;

        private readonly List<Question> _questions = [];
        public IReadOnlyList<Question> Questions => _questions.AsReadOnly();
        
        private QuizSet()
        {} // ef

        private QuizSet(Guid id, QuizSetCreationParams args) : base(id)
        {
            OwnerId = args.OwnerId;
            Title = args.Title;
            Description = args.Description;
        }

        public static ErrorOr<QuizSet> Create(QuizSetCreationParams args)
        {
            var validationResult = ValidateInvariants(args);

            if (validationResult.IsError)
                return validationResult.Errors;

            return new QuizSet(Guid.CreateVersion7(), args);
        }

        public ErrorOr<Updated> AddQuestion(QuestionCreationParams args)
        {
            var questionResult = Question.Create(args);

            if (questionResult.IsError)
                return questionResult.Errors;

            _questions.Add(questionResult.Value);

            return Result.Updated;
        }
        
        public ErrorOr<Updated> RemoveQuestion(Guid questionId)
        {
            var question = _questions.FirstOrDefault(q => q.Id == questionId);

            if (question is null)
                return Error.NotFound("QuizSet.QuestionNotFound", "Question not found in this quiz set.");

            _questions.Remove(question);

            return Result.Updated;
        }
        
        public ErrorOr<Updated> UpdateDetails(string title, string description)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(title))
                errors.Add(Error.Validation("QuizSet.TitleRequired", "QuizSet Title is required"));

            if (string.IsNullOrWhiteSpace(description))
                errors.Add(Error.Validation("QuizSet.DescriptionRequired", "QuizSet Description is required"));

            if (errors.Count > 0)
                return errors;

            Title = title;
            Description = description;

            return Result.Updated;
        }

        public ErrorOr<Updated> Publish()
        {
            Visibility = Visibility.Public;

            return Result.Updated;
        }

        public ErrorOr<Updated> Unpublish()
        {
            Visibility = Visibility.Private;

            return Result.Updated;
        }

        private static ErrorOr<Success> ValidateInvariants(QuizSetCreationParams args)
        {
            var errors = new List<Error>();

            if (string.IsNullOrWhiteSpace(args.Title))
                errors.Add(Error.Validation("QuizSet.TitleRequired", "QuizSet Title is required"));

            if (string.IsNullOrWhiteSpace(args.Description))
                errors.Add(Error.Validation("QuizSet.DescriptionRequired", "QuizSet Description is required"));

            return errors.Count > 0 ? errors : Result.Success;
        }
    }