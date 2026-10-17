using System.Collections.Concurrent;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

// Must be registered as a singleton: it is the store, not a connection to one.
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<DomainEvent>> _eventStore = new();

    public Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default
    )
    {
        var stream = _eventStore.GetOrAdd(streamName, _ => []);
        lock (stream)
        {
            if (stream.Count != expectedVersion)
                throw new ConcurrencyException(streamName, expectedVersion);

            foreach (var domainEvent in events)
            {
                domainEvent.Version = stream.Count + 1;
                stream.Add(domainEvent);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int fromVersion = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (_eventStore.TryGetValue(streamName, out var stream))
        {
            lock (stream)
            {
                return Task.FromResult<IReadOnlyCollection<DomainEvent>>(
                    [.. stream.Skip(Math.Max(fromVersion - 1, 0))]
                );
            }
        }

        return Task.FromResult<IReadOnlyCollection<DomainEvent>>([]);
    }
}
