using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Core.Abstractions;

public interface IEventSerializer
{
    string Serialize(DomainEvent domainEvent);
    DomainEvent Deserialize(string json);
}
