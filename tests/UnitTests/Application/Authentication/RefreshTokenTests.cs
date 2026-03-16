using Ratatosk.Application.Authentication.Models;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class RefreshTokenTests
{
    [TestMethod]
    public void IsExpired_Should_Return_False_When_Token_Has_Not_Expired()
    {
        var token = new RefreshToken("token", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));

        Assert.IsFalse(token.IsExpired);
    }

    [TestMethod]
    public void IsExpired_Should_Return_True_When_Token_Has_Expired()
    {
        var token = new RefreshToken("token", "user@example.com", DateTimeOffset.UtcNow.AddDays(-1));

        Assert.IsTrue(token.IsExpired);
    }

    [TestMethod]
    public void IsExpired_Should_Return_True_When_Token_Expires_At_Exact_Now()
    {
        var token = new RefreshToken("token", "user@example.com", DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.IsTrue(token.IsExpired);
    }
}
