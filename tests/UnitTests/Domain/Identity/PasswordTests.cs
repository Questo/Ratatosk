using Ratatosk.Domain.Identity;

namespace Ratatosk.UnitTests.Domain.Identity;

[TestClass]
public class PasswordTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void Create_WithInValidInput_ShouldFail(string input)
    {
        var result = Password.Create(input);
        Assert.IsTrue(result.IsFailure, $"Expected failture for Password: {input}");
    }

    [TestMethod]
    [DataRow("password")]
    [DataRow("very strong password")]
    public void Create_WithValidInput_ShouldSucceed(string input)
    {
        var result = Password.Create(input);
        Assert.IsTrue(result.IsSuccess, $"Expected success for Password: {input}");
    }
}
