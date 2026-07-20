using ErrorOr;
using Mediator;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<ErrorOr<TokenPair>>;