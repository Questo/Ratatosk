using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.EventStore;

public class FileEventStore(EventStoreOptions options, IEventSerializer serializer) : IEventStore
{
    private readonly string _directory = options.FilePath ?? "data/event-store";

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_directory))
            Directory.CreateDirectory(_directory);
    }

    private string GetStreamFilePath(string streamName) =>
        Path.Combine(_directory, $"{streamName}.log");

    public async Task AppendEventsAsync(string streamName, IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
    {
        var filePath = GetStreamFilePath(streamName);
        EnsureDirectoryExists();

        var lines = events
            .Select(serializer.Serialize)
            .Select(serialized => serialized + Environment.NewLine);

        await File.AppendAllLinesAsync(filePath, lines, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(string streamName, CancellationToken cancellationToken = default)
    {
        var filePath = GetStreamFilePath(streamName);
        if (!File.Exists(filePath))
            return [];

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var events = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(serializer.Deserialize)
            .ToList();

        return events;
    }
}
