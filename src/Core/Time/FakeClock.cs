using Ratatosk.Core.Abstractions;

namespace Ratatosk.Core.Time;

public class FakeClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset _now = now;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
    }

    public void Set(DateTimeOffset newNow)
    {
        _now = newNow;
    }
}
