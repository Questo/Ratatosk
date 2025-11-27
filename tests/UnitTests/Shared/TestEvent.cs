using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.UnitTests.Shared;

public class TestEvent(string value) : DomainEvent
{
    public string Value { get; } = value;
}
