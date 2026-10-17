using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.Serialization.Serializers;

public class JsonEventSerializer : JsonPolymorphicSerializer<DomainEvent>, IEventSerializer
{
    protected override IEnumerable<string> GetPreferredPropertyOrder() =>
        [nameof(DomainEvent.Version), nameof(DomainEvent.OccurredAtUtc)];
}
