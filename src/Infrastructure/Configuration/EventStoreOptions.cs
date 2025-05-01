namespace Ratatosk.Infrastructure.Configuration;

public class EventStoreOptions
{
    public const string SectionName = "EventStore";

    public EventStoreType Type { get; set; } = EventStoreType.InMemory;
    public string? FilePath { get; set; }
    public string? ConnectionString { get; set; }
}

public enum EventStoreType
{
    InMemory,
    File,
    Sql
}