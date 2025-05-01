using System.Collections.Concurrent;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<DomainEvent>> _eventStore = new();

    public Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, int startingVersion, CancellationToken cancellationToken = default)
    {
        var stream = _eventStore.GetOrAdd(streamName, _ => []);
        lock (stream)
        {
            stream.AddRange(events);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, int startingVersion = 0, CancellationToken cancellationToken = default)
    {
        if (_eventStore.TryGetValue(streamName, out var stream))
        {
            lock (stream)
            {
                return Task.FromResult<IReadOnlyCollection<DomainEvent>>([.. stream]);
            }
        }

        return Task.FromResult<IReadOnlyCollection<DomainEvent>>([]);
    }
}
