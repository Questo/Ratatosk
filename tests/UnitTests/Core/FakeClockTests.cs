using Ratatosk.Core.Time;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class FakeClockTests
{
    [TestMethod]
    public void UtcNow_Should_Return_Initial_Value()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2025, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(initialTime);

        // Act
        var now = clock.UtcNow;

        // Assert
        Assert.AreEqual(initialTime, now);
    }

    [TestMethod]
    public void Advance_Should_Advance_Time_By_Duration()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2025, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(initialTime);

        // Act
        clock.Advance(TimeSpan.FromHours(2));
        var now = clock.UtcNow;

        // Assert
        Assert.AreEqual(initialTime.AddHours(2), now);
    }

    [TestMethod]
    public void Set_Should_Override_Clock_Time()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2025, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var newTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(initialTime);

        // Act
        clock.Set(newTime);
        var now = clock.UtcNow;

        // Assert
        Assert.AreEqual(newTime, now);
    }
}
