using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence.EventStore;

public class FileEventStore(EventStoreOptions options, IEventSerializer serializer) : IEventStore
{
    // One lock for every stream file: appends are rare and this keeps the version check atomic.
    private static readonly SemaphoreSlim AppendLock = new(1, 1);

    private readonly string _directory = options.FilePath ?? "data/event-store";

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_directory))
            Directory.CreateDirectory(_directory);
    }

    private string GetStreamFilePath(string streamName) =>
        Path.Combine(_directory, $"{streamName}.log");

    public async Task AppendEventsAsync(
        string streamName,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default
    )
    {
        var filePath = GetStreamFilePath(streamName);
        EnsureDirectoryExists();

        await AppendLock.WaitAsync(cancellationToken);
        try
        {
            var currentVersion = (await ReadLinesAsync(filePath, cancellationToken)).Count;
            if (currentVersion != expectedVersion)
                throw new ConcurrencyException(streamName, expectedVersion);

            var lines = events
                .Select((domainEvent, i) =>
                {
                    domainEvent.Version = expectedVersion + i + 1;
                    return serializer.Serialize(domainEvent);
                })
                .ToList();

            await File.AppendAllLinesAsync(filePath, lines, cancellationToken);
        }
        finally
        {
            AppendLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
        string streamName,
        int fromVersion = 1,
        CancellationToken cancellationToken = default
    )
    {
        var lines = await ReadLinesAsync(GetStreamFilePath(streamName), cancellationToken);

        return [.. lines.Skip(Math.Max(fromVersion - 1, 0)).Select(serializer.Deserialize)];
    }

    private static async Task<List<string>> ReadLinesAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(filePath))
            return [];

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        return [.. lines.Where(line => !string.IsNullOrWhiteSpace(line))];
    }
}
