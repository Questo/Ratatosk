using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ratatosk.Application;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Ordering;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.EventStore;
using Ratatosk.Infrastructure.Persistence.Repositories;
using Ratatosk.Infrastructure.Shared;

namespace Ratatosk.UnitTests.Shared;

/// <summary>
/// The application wired as in production (dispatcher, handlers, aggregate repository, event
/// bus, per-handler dispatch), with in-memory adapters at the storage seams. Tests send commands
/// and assert on streams and read models, never on which collaborator was called.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    public InMemoryEventStore Events { get; }
    public InMemorySnapshotStore Snapshots { get; }
    public InMemoryProductReadModelRepository Products { get; }
    public InMemoryInventoryReadModelRepository Stock { get; }
    public InMemoryOrderReadModelRepository Orders { get; }
    public TransactionLog Transactions { get; }
    public ListLoggerProvider Logs { get; }

    private TestHost(ServiceProvider provider, ListLoggerProvider logs)
    {
        _provider = provider;
        Logs = logs;
        Events = provider.GetRequiredService<InMemoryEventStore>();
        Snapshots = provider.GetRequiredService<InMemorySnapshotStore>();
        Products = provider.GetRequiredService<InMemoryProductReadModelRepository>();
        Stock = provider.GetRequiredService<InMemoryInventoryReadModelRepository>();
        Orders = provider.GetRequiredService<InMemoryOrderReadModelRepository>();
        Transactions = provider.GetRequiredService<TransactionLog>();
    }

    public static async Task<TestHost> StartAsync(Action<IServiceCollection>? configure = null)
    {
        var logs = new ListLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddApplication();

        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<InMemoryEventStore>();
        services.AddSingleton<InMemorySnapshotStore>();
        services.AddScoped<IEventStore>(sp => sp.GetRequiredService<InMemoryEventStore>());
        services.AddScoped<ISnapshotStore>(sp => sp.GetRequiredService<InMemorySnapshotStore>());
        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));

        services.AddSingleton<TransactionLog>();
        services.AddScoped<IUnitOfWork, RecordingUnitOfWork>();

        services.AddSingleton<InMemoryProductReadModelRepository>();
        services.AddSingleton<IProductReadModelRepository>(sp =>
            sp.GetRequiredService<InMemoryProductReadModelRepository>()
        );
        services.AddSingleton<IProductDomainService>(sp =>
            sp.GetRequiredService<InMemoryProductReadModelRepository>()
        );
        services.AddSingleton<InMemoryInventoryReadModelRepository>();
        services.AddSingleton<IInventoryReadModelRepository>(sp =>
            sp.GetRequiredService<InMemoryInventoryReadModelRepository>()
        );
        services.AddSingleton<InMemoryOrderReadModelRepository>();
        services.AddSingleton<IOrderReadModelRepository>(sp =>
            sp.GetRequiredService<InMemoryOrderReadModelRepository>()
        );

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider(validateScopes: true);
        var dispatch = ActivatorUtilities.CreateInstance<ProjectionRegistrationService>(provider);
        await dispatch.StartAsync(CancellationToken.None);

        return new TestHost(provider, logs);
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        return await dispatcher.DispatchAsync(request);
    }

    public async Task<T> LoadAsync<T>(Guid id)
        where T : AggregateRoot, new()
    {
        using var scope = _provider.CreateScope();
        var result = await scope
            .ServiceProvider.GetRequiredService<IAggregateRepository<T>>()
            .LoadAsync(id);
        result.EnsureSuccess();
        return result.Value!;
    }

    public async Task CommitAsync<T>(T aggregate)
        where T : AggregateRoot, new()
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAggregateRepository<T>>().CommitAsync(aggregate);
    }

    public async Task<IReadOnlyList<DomainEvent>> StreamAsync<T>(Guid id)
        where T : AggregateRoot
    {
        var events = await Events.LoadEventsAsync(StreamName.For<T>(id));
        return [.. events];
    }

    public T GetService<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose() => _provider.Dispose();
}

/// <summary>Every commit and rollback across all scopes, in order.</summary>
public sealed class TransactionLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_entries)
                return [.. _entries];
        }
    }

    public void Add(string entry)
    {
        lock (_entries)
            _entries.Add(entry);
    }
}

/// <summary>
/// A unit of work with nothing behind it: the in-memory stores aren't transactional. It records
/// when the transaction is ended so tests can check commit-before-publish ordering.
/// </summary>
public sealed class RecordingUnitOfWork(TransactionLog log) : IUnitOfWork
{
    public IDbConnection Connection =>
        throw new NotSupportedException("In-memory adapters don't use a connection.");

    public IDbTransaction Transaction =>
        throw new NotSupportedException("In-memory adapters don't use a transaction.");

    public void Begin() { }

    public void Commit() => log.Add("commit");

    public void Rollback() => log.Add("rollback");

    public void Dispose() { }
}

public sealed class ListLoggerProvider : ILoggerProvider
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ListLogger(Entries);

    public void Dispose() { }

    private sealed class ListLogger(List<(LogLevel, string, Exception?)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            lock (entries)
                entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
