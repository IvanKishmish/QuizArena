namespace QuizArena.Application.Common.Interfaces;

public interface IOutboxWriter
{
    void Enqueue<TMessage>(TMessage message) where TMessage : notnull;
}
