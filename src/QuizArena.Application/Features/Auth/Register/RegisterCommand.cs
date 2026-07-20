using ErrorOr;
using Mediator;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.Register;

public sealed record RegisterCommand(string NickName, string Email, string Password)
: ICommand<ErrorOr<TokenPair>>;