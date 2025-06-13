using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class EnumerationTests
{

    [TestMethod]
    public void Equals_WhenSameObject_ReturnsTrue()
    {
        var f1 = TestEnumeration.Foobar;

        Assert.IsTrue(f1.Equals(f1));
    }

    [TestMethod]
    public void Equals_WhenOtherObjectIsNull_ReturnsFalse()
    {
        var f1 = TestEnumeration.Foobar;

        Assert.IsFalse(f1.Equals(null));
    }

    [TestMethod]
    public void Equals_WhenDifferentEnums_ReturnsFalse()
    {
        var f1 = TestEnumeration.Foobar;
        var f2 = TestEnumeration.Barfoo;

        Assert.IsFalse(f1.Equals(f2));
    }
}

class TestEnumeration(int id, string name) : Enumeration(id, name)
{
    public static TestEnumeration Foobar = new(1, nameof(Foobar));
    public static TestEnumeration Barfoo = new(2, nameof(Barfoo));
}