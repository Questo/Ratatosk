namespace Ratatosk.Core.Abstractions;

/// <summary>
/// Thrown when events are appended to a stream at a version other than the one expected,
/// i.e. another writer committed to the same aggregate first.
/// </summary>
public sealed class ConcurrencyException(string streamName, int expectedVersion, Exception? inner = null)
    : Exception(
        $"Stream '{streamName}' is no longer at version {expectedVersion}; reload and retry.",
        inner
    )
{
    public string StreamName { get; } = streamName;
    public int ExpectedVersion { get; } = expectedVersion;
}
