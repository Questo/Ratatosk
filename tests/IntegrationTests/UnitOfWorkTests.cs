using Dapper;
using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class UnitOfWorkTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";

    [TestMethod]
    public async Task Commit_ShouldLeaveUnitOfWorkUsableForFurtherWork()
    {
        var uow = new UnitOfWork(ConnectionString);
        uow.Begin();

        await uow.Connection.ExecuteAsync(
            "CREATE TEMP TABLE uow_commit_test (id integer PRIMARY KEY)",
            transaction: uow.Transaction
        );

        await uow.Connection.ExecuteAsync(
            "INSERT INTO uow_commit_test (id) VALUES (1)",
            transaction: uow.Transaction
        );
        uow.Commit();

        await uow.Connection.ExecuteAsync(
            "INSERT INTO uow_commit_test (id) VALUES (2)",
            transaction: uow.Transaction
        );
        uow.Commit();

        var count = await uow.Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM uow_commit_test",
            transaction: uow.Transaction
        );

        Assert.AreEqual(2, count);

        uow.Dispose();
    }
}
