namespace Ratatosk.Infrastructure.Configuration;

public class EventStoreOptions
{
    public const string SectionName = "EventStore";

    public StoreType Type { get; set; } = StoreType.InMemory;
    public string? FilePath { get; set; }
    public string? ConnectionString { get; set; }
}

public enum StoreType
{
    InMemory,
    File,
    Sql
}