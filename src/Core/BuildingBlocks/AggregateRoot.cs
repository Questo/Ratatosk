using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.BuildingBlocks;

public abstract class AggregateRoot
{
    private readonly Stack<DomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; }
    public int Version { get; protected set; } = 0;

    public IReadOnlyCollection<DomainEvent> UncommittedEvents => [.. _uncommittedEvents];

    protected void RaiseEvent(DomainEvent domainEvent)
    {
        _uncommittedEvents.Push(domainEvent);
        ApplyEvent(domainEvent);
        Version++;
    }

    protected abstract void ApplyEvent(DomainEvent domainEvent);

    public static Result<T> Rehydrate<T>(IEnumerable<DomainEvent> history) where T : AggregateRoot, new()
    {
        if (history == null || !history.Any())
            return Result<T>.Failure("History cannot be null or empty");
        try
        {
            var aggregate = new T();
            aggregate.LoadFromHistory(history);
            aggregate.ClearUncommittedEvents();
            return Result<T>.Success(aggregate);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Failed to rehydrate aggregate: {ex.Message}");
        }
    }

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    public void LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyEvent(@event);
            Version++;
        }
    }
}
