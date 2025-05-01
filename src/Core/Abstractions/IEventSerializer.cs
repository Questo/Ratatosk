using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Infrastructure.EventStore;

public interface IEventSerializer
{
    string Serialize(DomainEvent domainEvent);
    DomainEvent Deserialize(string json);
}
