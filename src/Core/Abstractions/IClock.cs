namespace Ratatosk.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
