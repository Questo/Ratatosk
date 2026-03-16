using Dapper;
using Ratatosk.Application.Authentication.Models;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresRefreshTokenRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";

    private UnitOfWork _uow = null!;
    private PostgresRefreshTokenRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        await _uow.Connection.ExecuteAsync(
            """
            DROP TABLE IF EXISTS refresh_tokens;
            CREATE TABLE refresh_tokens (
                token text PRIMARY KEY,
                email text NOT NULL,
                expires_at timestamptz NOT NULL
            );
            """,
            transaction: _uow.Transaction
        );

        _repo = new PostgresRefreshTokenRepository(_uow);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_uow == null)
            return;

        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_And_GetByTokenAsync_Should_Return_Saved_Token()
    {
        var token = new RefreshToken("tok123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));

        await _repo.SaveAsync(token, TestContext.CancellationToken);
        var result = await _repo.GetByTokenAsync("tok123", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(token.Token, result.Token);
        Assert.AreEqual(token.Email, result.Email);
    }

    [TestMethod]
    public async Task GetByTokenAsync_Should_Return_Null_When_Token_Does_Not_Exist()
    {
        var result = await _repo.GetByTokenAsync("nonexistent", TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RevokeAsync_Should_Remove_Token()
    {
        var token = new RefreshToken("tok123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));

        await _repo.SaveAsync(token, TestContext.CancellationToken);
        await _repo.RevokeAsync("tok123", TestContext.CancellationToken);

        var result = await _repo.GetByTokenAsync("tok123", TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RevokeAsync_Should_Not_Throw_When_Token_Does_Not_Exist()
    {
        await _repo.RevokeAsync("nonexistent", TestContext.CancellationToken);

        var result = await _repo.GetByTokenAsync("nonexistent", TestContext.CancellationToken);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SaveAsync_Should_Not_Overwrite_Existing_Token()
    {
        var original = new RefreshToken("tok123", "user@example.com", DateTimeOffset.UtcNow.AddDays(7));
        var duplicate = new RefreshToken("tok123", "other@example.com", DateTimeOffset.UtcNow.AddDays(1));

        await _repo.SaveAsync(original, TestContext.CancellationToken);
        await _repo.SaveAsync(duplicate, TestContext.CancellationToken);

        var result = await _repo.GetByTokenAsync("tok123", TestContext.CancellationToken);
        Assert.AreEqual("user@example.com", result!.Email);
    }

    public TestContext TestContext { get; set; } = null!;
}
