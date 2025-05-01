using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.EventStore;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class JsonEventSerializer : JsonPolymorphicSerializer<DomainEvent>, IEventSerializer
{
}