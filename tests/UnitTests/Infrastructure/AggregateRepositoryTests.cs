using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence.Repositories;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Infrastructure;

[TestClass]
public class AggregateRepositoryTests
{
    private readonly InMemoryEventStore _events = new();
    private readonly InMemorySnapshotStore _snapshots = new();
    private readonly TransactionLog _log = new();
    private readonly RecordingBus _bus = new();

    private AggregateRepository<T> Repository<T>()
        where T : AggregateRoot, new() =>
        new(_events, _snapshots, new RecordingUnitOfWork(_log), _bus);

    [TestMethod]
    public async Task CommitAsync_CommitsBeforePublishing_AndClearsUncommittedEvents()
    {
        _bus.OnPublish = _ => _log.Add("publish");
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A product for testing", 10m).Value!;
        product.Update(Name("Renamed product"), null, null);

        await Repository<Product>().CommitAsync(product);

        CollectionAssert.AreEqual(new[] { "commit", "publish", "publish" }, _log.Entries.ToArray());
        Assert.IsEmpty(product.UncommittedEvents);
        Assert.AreEqual(product.Version, product.PersistedVersion);
    }

    [TestMethod]
    public async Task CommitAsync_WithNothingToCommit_DoesNothing()
    {
        var product = await CreateAsync();

        await Repository<Product>().CommitAsync(product);

        Assert.HasCount(1, _log.Entries);
    }

    [TestMethod]
    public async Task CommitAsync_WhenAnotherWriterCommittedFirst_ThrowsAndRollsBackWithoutPublishing()
    {
        var created = await CreateAsync();
        var first = (await Repository<Product>().LoadAsync(created.Id)).Value!;
        var second = (await Repository<Product>().LoadAsync(created.Id)).Value!;
        first.Update(Name("First writer"), null, null);
        second.Update(Name("Second writer"), null, null);
        await Repository<Product>().CommitAsync(first);
        _bus.Published.Clear();

        await Assert.ThrowsAsync<ConcurrencyException>(() => Repository<Product>().CommitAsync(second));

        Assert.AreEqual("rollback", _log.Entries[^1]);
        Assert.IsEmpty(_bus.Published);
        Assert.HasCount(1, second.UncommittedEvents, "a failed commit leaves the events for a retry");
        Assert.AreEqual("First writer", (await Repository<Product>().LoadAsync(created.Id)).Value!.Name.Value);
    }

    [TestMethod]
    public async Task CommitAsync_AcrossTheSnapshotFrequency_SavesASnapshotThatLoadRestoresFrom()
    {
        var product = await CreateAsync();
        for (var i = 1; i < product.SnapshotFrequency + 2; i++)
        {
            product.Update(Name($"Name {i}"), null, null);
            await Repository<Product>().CommitAsync(product);
        }

        var snapshot = await _snapshots.LoadSnapshotAsync(typeof(Product).FullName!, product.Id);
        Assert.IsInstanceOfType<ProductSnapshot>(snapshot);
        Assert.AreEqual(product.SnapshotFrequency, snapshot.Version);

        var loaded = (await Repository<Product>().LoadAsync(product.Id)).Value!;
        Assert.AreEqual(product.Version, loaded.Version);
        Assert.AreEqual(product.Name, loaded.Name);
        Assert.AreEqual(product.Sku, loaded.Sku);
    }

    [TestMethod]
    public async Task LoadAsync_IgnoresSnapshotsOfAnotherAggregateTypeWithTheSameId()
    {
        var product = await CreateAsync();
        var inventory = Inventory.Create(product.Id);
        await Repository<Inventory>().CommitAsync(inventory);
        await _snapshots.SaveSnapshotAsync(product.CreateSnapshot()!.WithType<Product>());

        var loaded = await Repository<Inventory>().LoadAsync(product.Id);

        Assert.IsTrue(loaded.IsSuccess);
        Assert.AreEqual(1, loaded.Value!.Version);
    }

    [TestMethod]
    public async Task LoadAsync_UnknownId_Fails()
    {
        var result = await Repository<Product>().LoadAsync(Guid.NewGuid());

        Assert.IsTrue(result.IsFailure);
    }

    private async Task<Product> CreateAsync()
    {
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A product for testing", 10m).Value!;
        await Repository<Product>().CommitAsync(product);
        return product;
    }

    private static ProductName Name(string name) => ProductName.Create(name).Value!;

    private sealed class RecordingBus : IEventBus
    {
        public List<DomainEvent> Published { get; } = [];
        public Action<DomainEvent>? OnPublish { get; set; }

        public Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(domainEvent);
            OnPublish?.Invoke(domainEvent);
            return Task.CompletedTask;
        }

        public void Subscribe(Func<DomainEvent, CancellationToken, Task> handler) { }
    }
}

internal static class SnapshotExtensions
{
    public static Snapshot WithType<T>(this Snapshot snapshot)
    {
        snapshot.AggregateType = typeof(T).FullName!;
        return snapshot;
    }
}
