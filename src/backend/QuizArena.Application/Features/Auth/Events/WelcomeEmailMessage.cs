namespace QuizArena.Application.Features.Auth.Events;

public sealed record WelcomeEmailMessage(Guid UserId, string Email, string NickName);
