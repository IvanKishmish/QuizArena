using System.Text.Json;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Persistence.Context;

namespace QuizArena.Persistence.Outbox;

public sealed class OutboxWriter(AppDbContext dbContext) : IOutboxWriter
{
    public void Enqueue<TMessage>(TMessage message) 
        where TMessage : notnull
    {
        var type = typeof(TMessage).FullName ?? typeof(TMessage).Name;
        var payload = JsonSerializer.Serialize(message, message.GetType());

        dbContext.OutboxMessages.Add(OutboxMessage.Create(type, payload));
    }
}
