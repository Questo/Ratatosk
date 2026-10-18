using Ratatosk.Application.Changes;
using Ratatosk.Infrastructure.Changes;

namespace Ratatosk.UnitTests.Infrastructure;

[TestClass]
public class InProcessChangeBroadcasterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly InProcessChangeBroadcaster _broadcaster = new();

    [TestMethod]
    public async Task APublishedSignal_ReachesEverySubscriber()
    {
        using var cts = new CancellationTokenSource();
        await using var first = _broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator();
        await using var second = _broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator();
        var signal = new ChangeSignal("product", Guid.NewGuid(), 1);

        _broadcaster.Publish(signal);

        Assert.AreEqual(signal, await NextAsync(first));
        Assert.AreEqual(signal, await NextAsync(second));
    }

    [TestMethod]
    public async Task ACancelledSubscriber_IsRemovedAndItsStreamEnds()
    {
        using var kept = new CancellationTokenSource();
        using var cancelled = new CancellationTokenSource();
        await using var keptStream = _broadcaster.SubscribeAsync(kept.Token).GetAsyncEnumerator();
        await using var cancelledStream = _broadcaster.SubscribeAsync(cancelled.Token).GetAsyncEnumerator();

        await cancelled.CancelAsync();
        var signal = new ChangeSignal("order", Guid.NewGuid(), 2);
        _broadcaster.Publish(signal);

        Assert.AreEqual(1, _broadcaster.SubscriberCount);
        Assert.IsFalse(await cancelledStream.MoveNextAsync().AsTask().WaitAsync(Timeout));
        Assert.AreEqual(signal, await NextAsync(keptStream));
    }

    [TestMethod]
    public async Task Publish_DropsSignalsForAFullSubscriber_WithoutBlocking()
    {
        using var cts = new CancellationTokenSource();
        await using var stalled = _broadcaster.SubscribeAsync(cts.Token).GetAsyncEnumerator();
        var id = Guid.NewGuid();

        var publishing = Task.Run(() =>
        {
            for (var version = 1; version <= InProcessChangeBroadcaster.BufferSize * 2; version++)
                _broadcaster.Publish(new ChangeSignal("stock", id, version));
        });
        await publishing.WaitAsync(Timeout);

        await cts.CancelAsync();
        var received = new List<int>();
        while (await stalled.MoveNextAsync().AsTask().WaitAsync(Timeout))
            received.Add(stalled.Current.Version);
        CollectionAssert.AreEqual(Enumerable.Range(1, InProcessChangeBroadcaster.BufferSize).ToArray(), received);
    }

    [TestMethod]
    public void Publish_WithNoSubscribers_DoesNothing()
    {
        _broadcaster.Publish(new ChangeSignal("product", Guid.NewGuid(), 1));

        Assert.AreEqual(0, _broadcaster.SubscriberCount);
    }

    private static async Task<ChangeSignal> NextAsync(IAsyncEnumerator<ChangeSignal> stream)
    {
        Assert.IsTrue(await stream.MoveNextAsync().AsTask().WaitAsync(Timeout));
        return stream.Current;
    }
}
