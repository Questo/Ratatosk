using Dapper;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
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
            Status = "Placed",
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
            Status = "Placed",
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

    [TestMethod]
    public async Task GetByCustomerAsync_ReturnsOnlyTheCustomersOrders_NewestFirst_WithLines()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(customer, placed, ("TS-AAAA0001", 1));
        var middle = await SaveOrderAsync(customer, placed.AddMinutes(1), ("TS-AAAA0002", 2), ("TS-AAAA0003", 3));
        var newest = await SaveOrderAsync(customer, placed.AddMinutes(2), ("TS-AAAA0004", 4));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(3), ("TS-AAAA0005", 5));

        var result = await _repo.GetByCustomerAsync(customer, cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { newest, middle, oldest },
            result.Items.Select(o => o.Id).ToArray()
        );
        Assert.AreEqual(3, result.TotalItems);
        Assert.IsTrue(result.Items.All(o => o.CustomerId == customer));
        CollectionAssert.AreEquivalent(
            new[] { "TS-AAAA0002", "TS-AAAA0003" },
            result.Items[1].Lines.Select(l => l.Sku).ToArray()
        );
        Assert.AreEqual(4, result.Items[0].Lines.Single().Quantity);
        Assert.AreEqual(9.99m, result.Items[2].Lines.Single().UnitPrice);
    }

    [TestMethod]
    public async Task GetByCustomerAsync_SecondPage_ReturnsTheRestWithLines()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(customer, placed, ("TS-AAAA0001", 1));
        await SaveOrderAsync(customer, placed.AddMinutes(1), ("TS-AAAA0002", 2));
        await SaveOrderAsync(customer, placed.AddMinutes(2), ("TS-AAAA0003", 3));

        var result = await _repo.GetByCustomerAsync(customer, 2, 2, TestContext.CancellationToken);

        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(2, result.PageSize);
        Assert.AreEqual(2, result.TotalPages);
        Assert.AreEqual(oldest, result.Items.Single().Id);
        Assert.AreEqual("TS-AAAA0001", result.Items.Single().Lines.Single().Sku);
    }

    [TestMethod]
    public async Task GetByCustomerAsync_WhenTheCustomerHasNoOrders_ReturnsAnEmptyPage()
    {
        await SaveOrderAsync(Guid.NewGuid(), DateTime.UtcNow, ("TS-AAAA0001", 1));

        var result = await _repo.GetByCustomerAsync(Guid.NewGuid(), cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(result.Items);
        Assert.AreEqual(0, result.TotalItems);
    }

    private async Task<Guid> SaveOrderAsync(
        Guid customerId,
        DateTime createdUtc,
        params (string Sku, int Quantity)[] lines
    )
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = "Placed",
            Lines = [.. lines.Select(l => new OrderLineReadModel(l.Sku, l.Quantity, 9.99m, "SEK"))],
            CreatedUtc = createdUtc,
            LastUpdatedUtc = createdUtc,
        };
        await _repo.SaveAsync(order, TestContext.CancellationToken);
        return order.Id;
    }

    public TestContext TestContext { get; set; }
}
