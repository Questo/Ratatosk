using Ratatosk.Core.Primitives;

namespace Ratatosk.UnitTests.Core;

[TestClass]
public class ErrorTests
{
    [TestMethod]
    public void None_Should_Have_Empty_Code_And_Message()
    {
        // Act
        var error = Error.None;

        // Assert
        Assert.AreEqual(string.Empty, error.Code);
        Assert.AreEqual(string.Empty, error.Message);
    }

    [TestMethod]
    public void Null_Should_Have_Code_Null_And_Custom_Message()
    {
        // Act
        var error = Error.Null("Parameter X cannot be null.");

        // Assert
        Assert.AreEqual("Null", error.Code);
        Assert.AreEqual("Parameter X cannot be null.", error.Message);
    }

    [TestMethod]
    public void FromException_Should_Use_Exception_Message()
    {
        // Arrange
        var ex = new InvalidOperationException("Something failed.");

        // Act
        var error = Error.FromException(ex);

        // Assert
        Assert.AreEqual("exception", error.Code);
        Assert.AreEqual("Something failed.", error.Message);
    }

    [TestMethod]
    public void Two_Identical_Errors_Should_Be_Equal()
    {
        // Arrange
        var e1 = new Error("code", "message");
        var e2 = new Error("code", "message");

        // Assert
        Assert.AreEqual(e1, e2);
        Assert.IsTrue(e1 == e2);
    }

    [TestMethod]
    public void Errors_With_Different_Data_Should_Not_Be_Equal()
    {
        var e1 = new Error("code1", "message1");
        var e2 = new Error("code2", "message2");

        Assert.AreNotEqual(e1, e2);
        Assert.IsTrue(e1 != e2);
    }
}
