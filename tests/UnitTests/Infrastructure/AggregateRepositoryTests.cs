using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence.Repositories;
using Ratatosk.Infrastructure.Shared;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Infrastructure;

[TestClass]
public class AggregateRepositoryTests
{
    private readonly InMemoryEventStore _events = new();
    private readonly InMemorySnapshotStore _snapshots = new();
    private readonly TransactionLog _log = new();
    private readonly RecordingBus _bus = new();

    private readonly ListLoggerProvider _logs = new();

    private AggregateRepository<T> Repository<T>(
        IEventStore? events = null,
        ISnapshotStore? snapshots = null,
        IUnitOfWork? uow = null
    )
        where T : AggregateRoot, new() =>
        new(
            events ?? _events,
            snapshots ?? _snapshots,
            uow ?? new RecordingUnitOfWork(_log),
            _bus,
            new LoggerFactory([_logs]).CreateLogger<AggregateRepository<T>>()
        );

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
    public async Task CommitAsync_WhenTheSnapshotCannotBeSaved_StillCommitsAndPublishes()
    {
        var product = await CreateAsync();
        var repository = Repository<Product>(snapshots: new FailingSnapshotStore());
        while (product.Version < product.SnapshotFrequency - 1)
        {
            product.Update(Name($"Name {product.Version}"), null, null);
            await repository.CommitAsync(product);
        }
        _bus.Published.Clear();

        product.Update(Name("Crosses the snapshot boundary"), null, null);
        await repository.CommitAsync(product);

        Assert.AreEqual(product.SnapshotFrequency, product.Version);
        Assert.HasCount(1, _bus.Published);
        Assert.AreEqual(product.Version, (await Repository<Product>().LoadAsync(product.Id)).Value!.Version);
        Assert.AreEqual("rollback", _log.Entries[^1], "the failed snapshot's transaction is rolled back");
        Assert.IsTrue(_logs.Entries.Any(e => e.Level == LogLevel.Warning && e.Exception is not null));
    }

    [TestMethod]
    public async Task CommitAsync_WhenTheTransactionFailsAfterAnAppendOutsideIt_PublishesTheStoredEventsAndThrows()
    {
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A product for testing", 10m).Value!;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Repository<Product>(uow: new FailingUnitOfWork()).CommitAsync(product)
        );

        Assert.HasCount(1, await _events.LoadEventsAsync(StreamName.For<Product>(product.Id)));
        Assert.HasCount(1, _bus.Published, "stored events must reach the read models");
        Assert.IsEmpty(product.UncommittedEvents);
    }

    [TestMethod]
    public async Task CommitAsync_WhenTheTransactionFailsAfterAnAppendInsideIt_ThrowsWithoutPublishing()
    {
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A product for testing", 10m).Value!;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Repository<Product>(events: new TransactionalEventStore(_events), uow: new FailingUnitOfWork())
                .CommitAsync(product)
        );

        Assert.IsEmpty(_bus.Published);
        Assert.HasCount(1, product.UncommittedEvents, "nothing was stored, so the events stay for a retry");
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

    [TestMethod]
    public async Task LoadHistoryAsync_WhenASnapshotExists_StillReturnsTheFullStreamFromVersionOne()
    {
        var product = await CreateAsync();
        for (var i = 1; i < product.SnapshotFrequency + 2; i++)
        {
            product.Update(Name($"Name {i}"), null, null);
            await Repository<Product>().CommitAsync(product);
        }
        Assert.IsNotNull(await _snapshots.LoadSnapshotAsync(typeof(Product).FullName!, product.Id));

        var history = await Repository<Product>().LoadHistoryAsync(product.Id);

        Assert.IsTrue(history.IsSuccess);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, product.Version).ToArray(),
            history.Value!.Select(e => e.Version).ToArray()
        );
    }

    [TestMethod]
    public async Task LoadHistoryAsync_UnknownId_FailsLikeLoadAsync()
    {
        var id = Guid.NewGuid();

        var result = await Repository<Product>().LoadHistoryAsync(id);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual($"Product with ID {id} not found", result.Error);
    }

    private async Task<Product> CreateAsync()
    {
        var product = Product.Create("Test Product", SkuGenerator.Generate("TS"), "A product for testing", 10m).Value!;
        await Repository<Product>().CommitAsync(product);
        return product;
    }

    private static ProductName Name(string name) => ProductName.Create(name).Value!;

    private sealed class FailingSnapshotStore : ISnapshotStore
    {
        public Task SaveSnapshotAsync(Snapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new IOException("disk full");

        public Task<Snapshot?> LoadSnapshotAsync(
            string aggregateType,
            Guid aggregateId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<Snapshot?>(null);
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public System.Data.IDbConnection Connection => throw new NotSupportedException();
        public System.Data.IDbTransaction Transaction => throw new NotSupportedException();

        public void Begin() { }

        public void Commit() => throw new InvalidOperationException("Failed to commit the transaction.");

        public void Rollback() { }

        public void Dispose() { }
    }

    // Stands in for the Postgres store: appends that only last if the transaction commits.
    private sealed class TransactionalEventStore(IEventStore inner) : IEventStore
    {
        public bool AppendsInTransaction => true;

        public Task AppendEventsAsync(
            string streamName,
            IEnumerable<DomainEvent> events,
            int expectedVersion,
            CancellationToken cancellationToken = default
        ) => inner.AppendEventsAsync(streamName, events, expectedVersion, cancellationToken);

        public Task<IReadOnlyCollection<DomainEvent>> LoadEventsAsync(
            string streamName,
            int fromVersion = 1,
            CancellationToken cancellationToken = default
        ) => inner.LoadEventsAsync(streamName, fromVersion, cancellationToken);
    }

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
