using Ratatosk.Domain.Identity;

namespace Ratatosk.UnitTests.Domain.Identity;

[TestClass]
public class EmailTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("invalid-email")]
    [DataRow("invalid-email@")]
    public void Create_WithInValidInput_ShouldFail(string input)
    {
        var result = Email.Create(input);
        Assert.IsTrue(result.IsFailure, $"Expected failure for Email: {input}");
    }

    [TestMethod]
    [DataRow("test@example.com")]
    [DataRow("test@example.co")]
    public void Create_WithValidInput_ShouldSucceed(string input)
    {
        var result = Email.Create(input);
        Assert.IsTrue(result.IsSuccess, $"Expected success for Email: {input}");
    }

    [TestMethod]
    [DataRow("TEST@EXAMPLE.COM")]
    [DataRow("test@EXAMPLE.com")]
    public void Create_WithValidInput_ShouldReturnLowerCaseEmailAddress(string input)
    {
        var result = Email.Create(input);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(input.ToLower(), result.Value!.Value);
    }
}
