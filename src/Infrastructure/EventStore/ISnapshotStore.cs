namespace Ratatosk.Infrastructure.EventStore;

public interface ISnapshotStore
{
    Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken);
    Task<Snapshot?> GetSnapshotAsync(Guid aggregateId, CancellationToken cancellationToken);
}