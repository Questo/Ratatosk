using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class GuardTests
{
    [TestMethod]
    public void AgainstEmpty_Should_Throw_When_Guid_Is_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Guard.AgainstEmpty(Guid.Empty, nameof(AgainstEmpty_Should_Throw_When_Guid_Is_Empty))
        );
    }

    [TestMethod]
    public void AgainstEmpty_Should_Not_Throw_When_Guid_Is_Valid()
    {
        Guard.AgainstEmpty(Guid.NewGuid(), "validGuid");
    }

    [TestMethod]
    public void AgainstNegativeOrZero_Should_Throw_When_Zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstNegativeOrZero(0, nameof(AgainstNegativeOrZero_Should_Throw_When_Zero))
        );
    }

    [TestMethod]
    public void AgainstNegativeOrZero_Should_Throw_When_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstNegativeOrZero(
                -1,
                nameof(AgainstNegativeOrZero_Should_Throw_When_Negative)
            )
        );
    }

    [TestMethod]
    public void AgainstNegativeOrZero_Should_Not_Throw_When_Positive()
    {
        Guard.AgainstNegativeOrZero(10, "positive");
    }

    [TestMethod]
    public void AgainstNull_Should_Throw_When_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Guard.AgainstNull(null, nameof(AgainstNull_Should_Throw_When_Null))
        );
    }

    [TestMethod]
    public void AgainstNull_Should_Not_Throw_When_Not_Null()
    {
        Guard.AgainstNull(new object(), "notNull");
    }

    [TestMethod]
    public void AgainstNullOrEmpty_Should_Throw_When_Null()
    {
        Assert.Throws<ArgumentException>(() =>
            Guard.AgainstNullOrEmpty(null, nameof(AgainstNullOrEmpty_Should_Throw_When_Null))
        );
    }

    [TestMethod]
    public void AgainstNullOrEmpty_Should_Throw_When_Empty()
    {
        Assert.Throws<ArgumentException>(() =>
            Guard.AgainstNullOrEmpty("", nameof(AgainstNullOrEmpty_Should_Throw_When_Empty))
        );
    }

    [TestMethod]
    public void AgainstNullOrEmpty_Should_Throw_When_Whitespace()
    {
        Assert.Throws<ArgumentException>(() =>
            Guard.AgainstNullOrEmpty("   ", nameof(AgainstNullOrEmpty_Should_Throw_When_Whitespace))
        );
    }

    [TestMethod]
    public void AgainstNullOrEmpty_Should_Not_Throw_When_Valid_String()
    {
        Guard.AgainstNullOrEmpty("Valid", "validString");
    }

    [TestMethod]
    public void AgainstOutOfRange_Should_Throw_When_Below_Min()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstOutOfRange(2, 3, 5, nameof(AgainstOutOfRange_Should_Throw_When_Below_Min))
        );
    }

    [TestMethod]
    public void AgainstOutOfRange_Should_Throw_When_Above_Max()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstOutOfRange(10, 3, 5, nameof(AgainstOutOfRange_Should_Throw_When_Above_Max))
        );
    }

    [TestMethod]
    public void AgainstOutOfRange_Should_Not_Throw_When_Within_Range()
    {
        Guard.AgainstOutOfRange(4, 3, 5, "withinRange");
    }
}
