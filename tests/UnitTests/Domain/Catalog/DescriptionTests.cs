using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.UnitTests.Domain.Catalog;

[TestClass]
public class DescriptionTests
{
    [TestMethod]
    public void Create_WithValidDescription_ShouldSucceed()
    {
        var validText = new string('A', 100); // 100 characters of 'A'

        var result = Description.Create(validText);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(validText, result.Value!.Value);
    }

    [TestMethod]
    public void Create_WithHtml_ShouldSanitizeAndSucceed()
    {
        var input = "<p>This is a <strong>valid</strong> description with <em>HTML</em> tags.</p>";
        var expected = "This is a valid description with HTML tags.";

        var result = Description.Create(input);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expected, result.Value!.Value);
    }

    [TestMethod]
    public void Create_WithTooShortDescription_ShouldFail()
    {
        var shortText = "Too short";

        var result = Description.Create(shortText);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"Description must be at least 10 characters long", result.Error);
    }

    [TestMethod]
    public void Create_WithTooLongDescription_ShouldFail()
    {
        var longText = new string('A', 2000);

        var result = Description.Create(longText);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Description must not exceed 1000 characters", result.Error);
    }

    [TestMethod]
    public void Create_WithEmptyString_ShouldFail()
    {
        var result = Description.Create("");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("Description cannot be empty", result.Error);
    }
}
