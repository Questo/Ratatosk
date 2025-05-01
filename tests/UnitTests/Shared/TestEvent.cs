using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.UnitTests.Shared;

public class TestEvent(string name) : DomainEvent
{
    public string Name { get; } = name;
}