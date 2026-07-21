namespace QuizArena.Domain.Common;

public abstract class TransientEntity
{
    public Guid Id { get; protected init;} = Guid.CreateVersion7();
    
    protected TransientEntity() { }
    protected TransientEntity(Guid id) => Id = id;
}