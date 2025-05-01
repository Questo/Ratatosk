using System.Collections.Concurrent;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<DomainEvent>> _eventStore = new();

    public Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
    {
        var stream = _eventStore.GetOrAdd(streamName, _ => []);
        lock (stream)
        {
            stream.AddRange(events);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, CancellationToken cancellationToken = default)
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
