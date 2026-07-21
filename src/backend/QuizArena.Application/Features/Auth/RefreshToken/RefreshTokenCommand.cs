using ErrorOr;
using Mediator;
using QuizArena.Application.Features.Auth.Common;

namespace QuizArena.Application.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<ErrorOr<TokenPair>>;