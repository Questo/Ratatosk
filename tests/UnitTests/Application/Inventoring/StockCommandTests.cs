using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Inventoring.Events;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Inventoring;

[TestClass]
public class StockCommandTests
{
    private TestHost _host = null!;

    [TestInitialize]
    public async Task Setup() => _host = await TestHost.StartAsync();

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task Restock_Reserve_Release_UpdateTheStockRecord()
    {
        var (productId, _) = await _host.AddStockedProductAsync(available: 10);

        (await _host.SendAsync(new ReserveStockCommand(productId, 4))).EnsureSuccess();
        (await _host.SendAsync(new ReleaseStockCommand(productId, 1))).EnsureSuccess();

        var stock = await _host.Stock.GetByProductIdAsync(productId);
        Assert.AreEqual(10, stock!.Available);
        Assert.AreEqual(3, stock.Reserved);
    }

    [TestMethod]
    public async Task Reserve_MoreThanAvailable_FailsAndChangesNothing()
    {
        var (productId, _) = await _host.AddStockedProductAsync(available: 2);

        var result = await _host.SendAsync(new ReserveStockCommand(productId, 3));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(0, (await _host.Stock.GetByProductIdAsync(productId))!.Reserved);
    }

    [TestMethod]
    public async Task Release_CannotReleaseStockHeldByAnOrder()
    {
        var (productId, sku) = await _host.AddStockedProductAsync(available: 5);
        await _host.PlaceOrderAsync((sku, 5));

        var result = await _host.SendAsync(new ReleaseStockCommand(productId, 5));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(5, (await _host.Stock.GetByProductIdAsync(productId))!.Reserved);
    }

    [TestMethod]
    public async Task StockCommands_ForAProductWithoutAStockRecord_Fail()
    {
        var unknown = Guid.NewGuid();

        Assert.IsTrue((await _host.SendAsync(new RestockCommand(unknown, 1))).IsFailure);
        Assert.IsTrue((await _host.SendAsync(new ReserveStockCommand(unknown, 1))).IsFailure);
        Assert.IsTrue((await _host.SendAsync(new ReleaseStockCommand(unknown, 1))).IsFailure);
    }

    [TestMethod]
    public async Task Reserve_WhenAnotherWriterCommitsFirst_RetriesOnFreshStockLevels()
    {
        var interfere = new CompeteOnFirstAppend();
        using var host = await TestHost.StartAsync(interfere.Install);
        var (productId, _) = await host.AddStockedProductAsync(available: 10);
        interfere.Arm(() => host.SendAsync(new ReserveStockCommand(productId, 3)));

        var result = await host.SendAsync(new ReserveStockCommand(productId, 5));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(8, (await host.Stock.GetByProductIdAsync(productId))!.Reserved);
        Assert.HasCount(2, (await host.StreamAsync<Ratatosk.Domain.Inventoring.Inventory>(productId)).OfType<StockReserved>());
    }

    /// <summary>
    /// Once armed, the next append first runs a competing command to completion, so the append
    /// that triggered it is now at a stale version and fails as a real race would.
    /// </summary>
    private sealed class CompeteOnFirstAppend : IEventStore
    {
        private IEventStore _inner = null!;
        private Func<Task>? _competitor;

        public void Install(IServiceCollection services) =>
            services.AddScoped<IEventStore>(sp =>
            {
                _inner = sp.GetRequiredService<InMemoryEventStore>();
                return this;
            });

        public void Arm(Func<Task> competitor) => _competitor = competitor;

        public bool AppendsInTransaction => _inner.AppendsInTransaction;

        public async Task AppendEventsAsync(
            string streamName,
            IEnumerable<DomainEvent> events,
            int expectedVersion,
            CancellationToken cancellationToken = default
        )
        {
            if (Interlocked.Exchange(ref _competitor, null) is { } competitor)
                await competitor();

            await _inner.AppendEventsAsync(streamName, events, expectedVersion, cancellationToken);
        }

        public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
            string streamName,
            int fromVersion = 1,
            CancellationToken cancellationToken = default
        ) => _inner.LoadEventsAsync(streamName, fromVersion, cancellationToken);
    }
}
