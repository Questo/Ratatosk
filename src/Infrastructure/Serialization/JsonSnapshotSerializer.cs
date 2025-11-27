using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;

namespace Ratatosk.Infrastructure.Serialization;

public class JsonSnapshotSerializer : JsonPolymorphicSerializer<Snapshot>, ISnapshotSerializer
{
    protected override IEnumerable<string> GetPreferredPropertyOrder() =>
        [nameof(Snapshot.Version), nameof(Snapshot.TakenAtUtc)];
}
