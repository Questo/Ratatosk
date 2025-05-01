using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence;

public class AggregateRepository<T>(IEventStore eventStore) : IAggregateRepository<T> where T : AggregateRoot, new()
{
    public async Task<Result<T>> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var streamName = StreamName.For<T>(id);
        var events = await eventStore.LoadEventsAsync(streamName, cancellationToken);
        if (events.Count == 0)
            return Result<T>.Failure($"{typeof(T).Name} with ID {id} not found");

        var aggregate = AggregateRoot.Rehydrate<T>(events);
        return aggregate;
    }

    public async Task SaveAsync(T aggregate, CancellationToken cancellationToken = default)
    {
        var streamName = StreamName.For<T>(aggregate.Id);
        var uncommitted = aggregate.UncommittedEvents;
        await eventStore.AppendEventsAsync(streamName, uncommitted, cancellationToken);
    }
}