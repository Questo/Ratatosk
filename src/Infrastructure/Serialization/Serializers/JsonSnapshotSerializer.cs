using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.Serialization.Serializers;

public class JsonSnapshotSerializer : JsonPolymorphicSerializer<Snapshot>, ISnapshotSerializer
{
    protected override IEnumerable<string> GetPreferredPropertyOrder() =>
        [nameof(Snapshot.Version), nameof(Snapshot.TakenAtUtc)];
}
