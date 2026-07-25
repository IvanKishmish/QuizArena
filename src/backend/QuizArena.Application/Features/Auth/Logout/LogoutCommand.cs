using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand<ErrorOr<Success>>;