using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class JsonEventSerializer : JsonPolymorphicSerializer<DomainEvent>, IEventSerializer
{
    protected override IEnumerable<string> GetPreferredPropertyOrder() =>
    [nameof(DomainEvent.Version), nameof(DomainEvent.OccurredAtUtc)];
}