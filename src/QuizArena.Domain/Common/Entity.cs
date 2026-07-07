namespace QuizArena.Domain.Common;

public abstract class Entity : IAuditable
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
    
    public DateTimeOffset CreatedAt { get; protected init; }
    public Guid CreatedBy { get; protected init; }
    
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected Entity() 
    { } // ef

    protected Entity(Guid id) => Id = id;
}