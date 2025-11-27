using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public interface IEventStore
{
    Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int startingVersion,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int startingVersion = 0,
        CancellationToken cancellationToken = default
    );
}
