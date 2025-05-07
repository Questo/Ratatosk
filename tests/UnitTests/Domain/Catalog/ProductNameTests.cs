using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.UnitTests.Domain.Catalog;

[TestClass]
public class ProductNameTests
{
    [TestMethod]
    public void Create_WithValidNames_ShouldSucceed()
    {
        var names = new[]
        {
            "Golden Acorn Deluxe",
            "Winter Fur Cloak",
            "O'Nutty Trail Mix",
            "Berry & Bark Energy Bites",
            "Squirrel-Approved Hammock XL",
            "Moss-Crafted Nest & Blanket"
        };

        foreach (var name in names)
        {
            var result = ProductName.Create(name);

            Assert.IsTrue(result.IsSuccess, $"Expected success for name: '{name}'");
            Assert.AreEqual(name, result.Value!.Value);
        }
    }

    [TestMethod]
    public void Create_WithTooShortName_ShouldFail()
    {
        var result = ProductName.Create("Hi");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Invalid product name format", result.Error);
    }

    [TestMethod]
    public void Create_WithTooLongName_ShouldFail()
    {
        var longName = new string('A', 101);
        var result = ProductName.Create(longName);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Invalid product name format", result.Error);
    }

    [TestMethod]
    public void Create_WithInvalidCharacters_ShouldFail()
    {
        var invalidNames = new[]
        {
            "Deluxe Nut-Bomb 💥",     // Emoji
            "Hazelnut@Home",          // @ symbol
            "Red-Cap <Shroom>",       // HTML-like
            "Berries #1!",            // Hash and exclamation
            "Maple Syrup (Premium)"   // Parentheses
        };

        foreach (var name in invalidNames)
        {
            var result = ProductName.Create(name);

            Assert.IsTrue(result.IsFailure, $"Expected failure for name: '{name}'");
        }
    }

    [TestMethod]
    public void Create_WithWhitespaceOrNull_ShouldFail()
    {
        var cases = new[] { "", "   ", null };

        foreach (var input in cases)
        {
            var result = ProductName.Create(input!);

            Assert.IsTrue(result.IsFailure);
        }
    }

    [TestMethod]
    public void ToString_ShouldReturnValue()
    {
        var result = ProductName.Create("Premium Nut Basket");

        Assert.AreEqual("Premium Nut Basket", result.Value!.ToString());
    }
}