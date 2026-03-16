using Ratatosk.Application.Authentication.Models;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.UnitTests.Application.Authentication;

[TestClass]
public class InMemoryRefreshTokenRepositoryTests
{
    private InMemoryRefreshTokenRepository _repository = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new InMemoryRefreshTokenRepository();
    }

    [TestMethod]
    public async Task SaveAsync_And_GetByTokenAsync_Should_Return_Saved_Token()
    {
        var token = new RefreshToken("abc123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));

        await _repository.SaveAsync(token);
        var result = await _repository.GetByTokenAsync("abc123");

        Assert.IsNotNull(result);
        Assert.AreEqual(token.Token, result.Token);
        Assert.AreEqual(token.Email, result.Email);
        Assert.AreEqual(token.ExpiresAt, result.ExpiresAt);
    }

    [TestMethod]
    public async Task GetByTokenAsync_Should_Return_Null_When_Token_Does_Not_Exist()
    {
        var result = await _repository.GetByTokenAsync("nonexistent");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RevokeAsync_Should_Remove_Token()
    {
        var token = new RefreshToken("abc123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));

        await _repository.SaveAsync(token);
        await _repository.RevokeAsync("abc123");

        var result = await _repository.GetByTokenAsync("abc123");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RevokeAsync_Should_Not_Throw_When_Token_Does_Not_Exist()
    {
        await _repository.RevokeAsync("nonexistent");

        var result = await _repository.GetByTokenAsync("nonexistent");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveAsync_Should_Overwrite_Existing_Token_With_Same_Key()
    {
        var original = new RefreshToken("abc123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));
        var updated = new RefreshToken("abc123", "other@example.com", DateTimeOffset.UtcNow.AddDays(1));

        await _repository.SaveAsync(original);
        await _repository.SaveAsync(updated);

        var result = await _repository.GetByTokenAsync("abc123");
        Assert.AreEqual("other@example.com", result!.Email);
    }
}
