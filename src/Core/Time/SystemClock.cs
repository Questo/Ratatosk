using Ratatosk.Core.Abstractions;

namespace Ratatosk.Core.Time;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
