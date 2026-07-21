using Mediator;
using Microsoft.Extensions.Logging;

namespace QuizArena.Application.Common.Behaviours;

public sealed class LoggingBehaviour<TMessage, TResponse>(
    ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
: IPipelineBehavior<TMessage, TResponse>
where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next, 
        CancellationToken cancellationToken = default)
    {
        var messageName = typeof(TMessage).Name;
        
        logger.LogInformation("Handling {MessageName}", messageName);

        var response = await next(message, cancellationToken);

        logger.LogInformation("Handled {MessageName}", messageName);

        return response;
    }
}