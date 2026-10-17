using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class FileSnapshotStore(EventStoreOptions options, ISnapshotSerializer serializer)
    : ISnapshotStore
{
    private readonly string _directory = Path.Combine(
        options.FilePath ?? "data/event-store",
        "snapshots"
    );

    private string GetSnapshotFilePath(string aggregateType, Guid aggregateId) =>
        Path.Combine(_directory, $"{aggregateType}-{aggregateId}.json");

    public async Task SaveSnapshotAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(_directory);

        await File.WriteAllTextAsync(
            GetSnapshotFilePath(snapshot.AggregateType, snapshot.AggregateId),
            serializer.Serialize(snapshot),
            cancellationToken
        );
    }

    public async Task<Snapshot?> LoadSnapshotAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken = default
    )
    {
        var filePath = GetSnapshotFilePath(aggregateType, aggregateId);
        if (!File.Exists(filePath))
            return null;

        return serializer.Deserialize(await File.ReadAllTextAsync(filePath, cancellationToken));
    }
}
