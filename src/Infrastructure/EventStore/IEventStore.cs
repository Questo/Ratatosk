using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public interface IEventStore
{
    Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, CancellationToken cancellationToken = default);
}
