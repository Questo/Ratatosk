using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Application.Changes;
using Ratatosk.Application.Inventoring.Commands;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Inventoring;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;
using Ratatosk.UnitTests.Shared;

namespace Ratatosk.UnitTests.Application.Changes;

[TestClass]
public class ChangeSignalHandlerTests
{
    private TestHost _host = null!;
    private readonly SignalRecorder _signals = new();

    [TestInitialize]
    public async Task Setup() =>
        _host = await TestHost.StartAsync(services => services.AddSingleton<IChangeBroadcaster>(_signals));

    [TestCleanup]
    public void Cleanup() => _host.Dispose();

    [TestMethod]
    public async Task AddingAProduct_SignalsTheProductAtVersionOne()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 0);

        CollectionAssert.AreEqual(
            new[] { new ChangeSignal(ChangeSignalHandler.Product, productId, 1) },
            _signals.Signals.ToArray()
        );
    }

    [TestMethod]
    public async Task Restocking_SignalsTheStockAtTheInventorysNewVersion()
    {
        var (productId, _) = await _host.AddStockedProductAsync(onHand: 0);
        _signals.Signals.Clear();

        (await _host.SendAsync(new RestockCommand(productId, 5))).EnsureSuccess();

        var inventory = await _host.LoadAsync<Inventory>(productId);
        CollectionAssert.AreEqual(
            new[] { new ChangeSignal(ChangeSignalHandler.Stock, productId, inventory.Version) },
            _signals.Signals.ToArray()
        );
        Assert.AreEqual((await _host.Stock.GetByProductIdAsync(productId))!.Version, inventory.Version);
    }

    [TestMethod]
    public async Task PlacingAFillableOrder_SignalsTheOrderUpToItsConfirmation()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);

        var orderId = await _host.PlaceOrderAsync((sku, 3));

        var confirmed = (await _host.StreamAsync<Order>(orderId)).OfType<OrderConfirmed>().Single();
        var orderSignals = _signals.Signals.Where(s => s.Kind == ChangeSignalHandler.Order).ToList();
        Assert.IsTrue(orderSignals.All(s => s.Id == orderId));
        CollectionAssert.Contains(orderSignals, new ChangeSignal(ChangeSignalHandler.Order, orderId, confirmed.Version));
    }

    [TestMethod]
    public async Task TheOrderConfirmedSignal_GoesOutAfterTheReadModelShowsTheOrderConfirmed()
    {
        var (_, sku) = await _host.AddStockedProductAsync(onHand: 10);
        var statusWhenSignalled = new Dictionary<int, string?>();
        _signals.OnPublish = signal =>
        {
            if (signal.Kind == ChangeSignalHandler.Order)
                statusWhenSignalled[signal.Version] = _host
                    .Orders.GetByIdAsync(signal.Id)
                    .GetAwaiter()
                    .GetResult()
                    ?.Status;
        };

        var orderId = await _host.PlaceOrderAsync((sku, 3));

        var confirmed = (await _host.StreamAsync<Order>(orderId)).OfType<OrderConfirmed>().Single();
        Assert.AreEqual(nameof(OrderStatus.Confirmed), statusWhenSignalled[confirmed.Version]);
    }

    [TestMethod]
    public void ChangeSignalHandler_IsTheLastHandlerOfEverySignalledEvent()
    {
        var signalled = typeof(ChangeSignalHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>));

        using var scope = _host.GetService<IServiceScopeFactory>().CreateScope();
        foreach (var handlerType in signalled)
            Assert.IsInstanceOfType<ChangeSignalHandler>(
                scope.ServiceProvider.GetServices(handlerType).Last(),
                handlerType.GenericTypeArguments[0].Name
            );
    }

    public sealed class SignalRecorder : IChangeBroadcaster
    {
        public List<ChangeSignal> Signals { get; } = [];
        public Action<ChangeSignal>? OnPublish { get; set; }

        public void Publish(ChangeSignal signal)
        {
            Signals.Add(signal);
            OnPublish?.Invoke(signal);
        }

        public IAsyncEnumerable<ChangeSignal> SubscribeAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
