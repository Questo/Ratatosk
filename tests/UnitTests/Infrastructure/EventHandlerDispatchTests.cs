using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Infrastructure;

[TestClass]
public class EventHandlerDispatchTests
{
    [TestMethod]
    public async Task AFailingHandler_IsRolledBackAndLogged_WithoutStoppingTheEventsOtherHandlers()
    {
        var calls = new CallLog();
        using var host = await TestHost.StartAsync(services =>
        {
            services.AddSingleton(calls);
            services.AddScoped<IDomainEventHandler<Pinged>, FailingHandler>();
            services.AddScoped<IDomainEventHandler<Pinged>, SucceedingHandler>();
        });

        await host.GetService<IEventBus>().PublishAsync(new Pinged());

        CollectionAssert.AreEqual(new[] { nameof(FailingHandler), nameof(SucceedingHandler) }, calls.Names);
        CollectionAssert.AreEqual(new[] { "rollback", "commit" }, host.Transactions.Entries.ToArray());
        Assert.IsTrue(
            host.Logs.Entries.Any(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException)
        );
    }

    public sealed class Pinged : DomainEvent;

    public sealed class CallLog
    {
        public List<string> Names { get; } = [];
    }

    public sealed class FailingHandler(CallLog calls) : IDomainEventHandler<Pinged>
    {
        public Task WhenAsync(Pinged domainEvent, CancellationToken cancellationToken = default)
        {
            calls.Names.Add(nameof(FailingHandler));
            throw new InvalidOperationException("boom");
        }
    }

    public sealed class SucceedingHandler(CallLog calls) : IDomainEventHandler<Pinged>
    {
        public Task WhenAsync(Pinged domainEvent, CancellationToken cancellationToken = default)
        {
            calls.Names.Add(nameof(SucceedingHandler));
            return Task.CompletedTask;
        }
    }
}
