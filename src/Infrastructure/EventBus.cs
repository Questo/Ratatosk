using System.Collections.Concurrent;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure;

public class EventBus : IEventBus
{
    private readonly ConcurrentBag<Func<DomainEvent, CancellationToken, Task>> _subscribers = [];

    public void Subscribe(Func<DomainEvent, CancellationToken, Task> handler)
    {
        _subscribers.Add(handler);
    }

    public async Task PublishAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber(domainEvent, cancellationToken);
        }
    }
}
