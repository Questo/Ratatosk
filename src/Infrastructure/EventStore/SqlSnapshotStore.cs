
namespace Ratatosk.Infrastructure.EventStore;

public class SqlSnapshotStore : ISnapshotStore
{
    public Task<Snapshot?> GetSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}