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
                    last_updated_utc timestamptz NOT NULL,
                    cancellation_reason text
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
    public async Task SaveAsync_ShouldInsertTheCancellationReason()
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = "Cancelled",
            Lines = [new OrderLineReadModel("TS-ABCD1234", 2, 9.99m, "SEK")],
            CreatedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
            CancellationReason = "Unexpected failure",
        };

        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);
        Assert.AreEqual("Unexpected failure", fetched!.CancellationReason);
        var page = await _repo.GetPageAsync(status: "Cancelled", cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual("Unexpected failure", page.Items.Single().CancellationReason);
    }

    [TestMethod]
    public async Task SaveAsync_WhenRecordAlreadyExists_ShouldUpdateTheCancellationReason()
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
        Assert.IsNull((await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken))!.CancellationReason);

        order.Status = "Cancelled";
        order.CancellationReason = "Not enough stock available for SKU TS-ABCD1234";
        await _repo.SaveAsync(order, TestContext.CancellationToken);

        var fetched = await _repo.GetByIdAsync(order.Id, TestContext.CancellationToken);
        Assert.AreEqual("Not enough stock available for SKU TS-ABCD1234", fetched!.CancellationReason);
        var page = await _repo.GetPageAsync(cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(
            "Not enough stock available for SKU TS-ABCD1234",
            page.Items.Single(o => o.Id == order.Id).CancellationReason
        );
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetPageAsync_ForACustomer_ReturnsOnlyTheCustomersOrders_NewestFirst_WithLines()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(customer, placed, ("TS-AAAA0001", 1));
        var middle = await SaveOrderAsync(customer, placed.AddMinutes(1), ("TS-AAAA0002", 2), ("TS-AAAA0003", 3));
        var newest = await SaveOrderAsync(customer, placed.AddMinutes(2), ("TS-AAAA0004", 4));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(3), ("TS-AAAA0005", 5));

        var result = await _repo.GetPageAsync(customer, cancellationToken: TestContext.CancellationToken);

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
    public async Task GetPageAsync_ForACustomer_SecondPage_ReturnsTheRestWithLines()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(customer, placed, ("TS-AAAA0001", 1));
        await SaveOrderAsync(customer, placed.AddMinutes(1), ("TS-AAAA0002", 2));
        await SaveOrderAsync(customer, placed.AddMinutes(2), ("TS-AAAA0003", 3));

        var result = await _repo.GetPageAsync(customer, null, 2, 2, TestContext.CancellationToken);

        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(2, result.PageSize);
        Assert.AreEqual(2, result.TotalPages);
        Assert.AreEqual(oldest, result.Items.Single().Id);
        Assert.AreEqual("TS-AAAA0001", result.Items.Single().Lines.Single().Sku);
    }

    [TestMethod]
    public async Task GetPageAsync_ForACustomer_WhenTheCustomerHasNoOrders_ReturnsAnEmptyPage()
    {
        await SaveOrderAsync(Guid.NewGuid(), DateTime.UtcNow, ("TS-AAAA0001", 1));

        var result = await _repo.GetPageAsync(Guid.NewGuid(), cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(result.Items);
        Assert.AreEqual(0, result.TotalItems);
    }

    [TestMethod]
    public async Task GetPageAsync_WithoutFilters_ReturnsEveryOrder_NewestFirst_WithLines()
    {
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(Guid.NewGuid(), placed, ("TS-AAAA0001", 1));
        var middle = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(1), ("TS-AAAA0002", 2));
        var newest = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), ("TS-AAAA0003", 3));

        var result = await _repo.GetPageAsync(cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { newest, middle, oldest },
            result.Items.Select(o => o.Id).ToArray()
        );
        Assert.AreEqual(3, result.TotalItems);
        CollectionAssert.AreEqual(
            new[] { 3, 2, 1 },
            result.Items.Select(o => o.Lines.Single().Quantity).ToArray()
        );
    }

    [TestMethod]
    public async Task GetPageAsync_WithAStatus_ReturnsOnlyOrdersInThatStatus()
    {
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var confirmedOld = await SaveOrderAsync(Guid.NewGuid(), placed, "Confirmed", ("TS-AAAA0001", 1));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(1), "Cancelled", ("TS-AAAA0002", 1));
        var confirmedNew = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), "Confirmed", ("TS-AAAA0003", 1));

        var result = await _repo.GetPageAsync(status: "Confirmed", cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { confirmedNew, confirmedOld },
            result.Items.Select(o => o.Id).ToArray()
        );
        Assert.AreEqual(2, result.TotalItems);
    }

    [TestMethod]
    public async Task GetPageAsync_WithACustomerAndAStatus_AppliesBoth()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var match = await SaveOrderAsync(customer, placed, "Cancelled", ("TS-AAAA0001", 1));
        await SaveOrderAsync(customer, placed.AddMinutes(1), "Confirmed", ("TS-AAAA0002", 1));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), "Cancelled", ("TS-AAAA0003", 1));

        var result = await _repo.GetPageAsync(customer, "Cancelled", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(match, result.Items.Single().Id);
        Assert.AreEqual(1, result.TotalItems);
    }

    [TestMethod]
    public async Task GetPageAsync_WithAStatus_SecondPage_CountsOnlyMatchingOrders()
    {
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = await SaveOrderAsync(Guid.NewGuid(), placed, "Confirmed", ("TS-AAAA0001", 1));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(1), "Confirmed", ("TS-AAAA0002", 1));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), "Cancelled", ("TS-AAAA0003", 1));
        await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(3), "Confirmed", ("TS-AAAA0004", 1));

        var result = await _repo.GetPageAsync(null, "Confirmed", 2, 2, TestContext.CancellationToken);

        Assert.AreEqual(3, result.TotalItems);
        Assert.AreEqual(2, result.TotalPages);
        Assert.AreEqual(oldest, result.Items.Single().Id);
        Assert.AreEqual("TS-AAAA0001", result.Items.Single().Lines.Single().Sku);
    }

    [TestMethod]
    public async Task SearchAsync_MatchesPartialIdCustomerIdStatusAndLineSku_IgnoringCase()
    {
        var customer = Guid.NewGuid();
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var byCustomer = await SaveOrderAsync(customer, placed, "Placed", ("TS-AAAA0001", 1));
        var bySku = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(1), "Placed", ("TS-AAAA0002", 1), ("TS-FIND0001", 2));
        var byStatus = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), "Cancelled", ("TS-AAAA0003", 1));

        Assert.AreEqual(byCustomer, (await SearchAsync(customer.ToString().ToUpperInvariant()[..13])).Single().Id);
        Assert.AreEqual(bySku, (await SearchAsync("ts-find")).Single().Id);
        Assert.AreEqual(byStatus, (await SearchAsync("CANCEL")).Single().Id);
        Assert.AreEqual(byStatus, (await SearchAsync(byStatus.ToString()[..6].ToUpperInvariant())).Single().Id);
        Assert.AreEqual(bySku, (await SearchAsync(bySku.ToString())).Single().Id);
        Assert.IsEmpty(await SearchAsync("no-such-thing"));
    }

    [TestMethod]
    public async Task SearchAsync_ReturnsAtMostTheLimit_NewestFirst_WithLines()
    {
        var placed = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await SaveOrderAsync(Guid.NewGuid(), placed, "Confirmed", ("TS-AAAA0001", 1));
        var middle = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(1), "Confirmed", ("TS-AAAA0002", 2), ("TS-AAAA0003", 3));
        var newest = await SaveOrderAsync(Guid.NewGuid(), placed.AddMinutes(2), "Confirmed", ("TS-AAAA0004", 4));

        var result = await _repo.SearchAsync("confirmed", 2, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { newest, middle }, result.Select(o => o.Id).ToArray());
        Assert.AreEqual(4, result[0].Lines.Single().Quantity);
        CollectionAssert.AreEquivalent(
            new[] { "TS-AAAA0002", "TS-AAAA0003" },
            result[1].Lines.Select(l => l.Sku).ToArray()
        );
        Assert.AreEqual(9.99m, result[1].Lines[0].UnitPrice);
    }

    private Task<IReadOnlyList<OrderReadModel>> SearchAsync(string term) =>
        _repo.SearchAsync(term, 10, TestContext.CancellationToken);

    private Task<Guid> SaveOrderAsync(
        Guid customerId,
        DateTime createdUtc,
        params (string Sku, int Quantity)[] lines
    ) => SaveOrderAsync(customerId, createdUtc, "Placed", lines);

    private async Task<Guid> SaveOrderAsync(
        Guid customerId,
        DateTime createdUtc,
        string status,
        params (string Sku, int Quantity)[] lines
    )
    {
        var order = new OrderReadModel
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = status,
            Lines = [.. lines.Select(l => new OrderLineReadModel(l.Sku, l.Quantity, 9.99m, "SEK"))],
            CreatedUtc = createdUtc,
            LastUpdatedUtc = createdUtc,
        };
        await _repo.SaveAsync(order, TestContext.CancellationToken);
        return order.Id;
    }

    public TestContext TestContext { get; set; }
}
