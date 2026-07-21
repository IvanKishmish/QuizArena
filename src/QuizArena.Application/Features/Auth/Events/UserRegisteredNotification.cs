using Mediator;

namespace QuizArena.Application.Features.Auth.Events;

public sealed record UserRegisteredNotification(Guid UserId, string Email, string NickName) : INotification;