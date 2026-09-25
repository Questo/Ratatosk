using Dapper;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.ReadModels;

namespace Ratatosk.IntegrationTests;

[TestClass]
public class PostgresOrderReadModelRepositoryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=ratatosk_test;Username=testuser;Password=testpass";
    private IUnitOfWork _uow = null!;
    private IOrderReadModelRepository _repo = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _uow = new UnitOfWork(ConnectionString);
        _uow.Begin();

        await _uow.Connection.ExecuteAsync(
            """
                DROP TABLE IF EXISTS order_line_read_models;
                DROP TABLE IF EXISTS order_read_models;
                CREATE TABLE IF NOT EXISTS order_read_models(
                    id uuid PRIMARY KEY,
                    customer_id uuid NOT NULL,
                    status text NOT NULL,
                    created_utc timestamptz NOT NULL,
                    last_updated_utc timestamptz NOT NULL
                );
                CREATE TABLE IF NOT EXISTS order_line_read_models(
                    order_id uuid NOT NULL REFERENCES order_read_models(id),
                    sku text NOT NULL,
                    quantity integer NOT NULL,
                    unit_price decimal NOT NULL,
                    currency text NOT NULL
                );
            """,
            transaction: _uow.Transaction
        );

        _repo = new OrderReadModelRepository(_uow);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_uow == null)
        {
            return;
        }

        _uow.Rollback();
        _uow.Dispose();
    }

    [TestMethod]
    public async Task SaveAsync_ShouldInsertOrderWithLines()
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = "Created",
            Lines = [new OrderLineReadModel("TS-ABCD1234", 2, 9.99m, "SEK")],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
        };

        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);

        Assert.IsNotNull(fetched);
        Assert.AreEqual(order.CustomerId, fetched!.CustomerId);
        Assert.AreEqual(1, fetched.Lines.Count);
        Assert.AreEqual("TS-ABCD1234", fetched.Lines[0].Sku);
    }

    [TestMethod]
    public async Task SaveAsync_WhenRecordAlreadyExists_ShouldReplaceLines()
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = "Created",
            Lines = [new OrderLineReadModel("TS-ABCD1234", 2, 9.99m, "SEK")],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
        };
        await _repo.SaveAsync(order, TestContext.CancellationToken);

        order.Status = "Confirmed";
        order.Lines = [new OrderLineReadModel("TS-ABCD1234", 5, 9.99m, "SEK")];
        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);

        Assert.IsNotNull(fetched);
        Assert.AreEqual("Confirmed", fetched!.Status);
        Assert.AreEqual(1, fetched.Lines.Count);
        Assert.AreEqual(5, fetched.Lines[0].Quantity);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    public TestContext TestContext { get; set; }
}
