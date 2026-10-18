using System.Collections.Concurrent;
using System.Threading.Channels;
using Ratatosk.Application.Changes;

namespace Ratatosk.Infrastructure.Changes;

/// <summary>
/// Fans change signals out to the subscribers of this process only, so live updates need a single
/// API instance (ADR 0002). Each subscriber reads from its own bounded channel; when a subscriber
/// falls behind, its newest signals are dropped rather than holding up the publisher, which runs
/// inside the command path.
/// </summary>
public sealed class InProcessChangeBroadcaster : IChangeBroadcaster
{
    public const int BufferSize = 100;

    private readonly ConcurrentDictionary<Channel<ChangeSignal>, byte> _subscribers = new();

    public int SubscriberCount => _subscribers.Count;

    public void Publish(ChangeSignal signal)
    {
        foreach (var subscriber in _subscribers.Keys)
            subscriber.Writer.TryWrite(signal);
    }

    public IAsyncEnumerable<ChangeSignal> SubscribeAsync(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<ChangeSignal>(
            new BoundedChannelOptions(BufferSize)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            }
        );

        // Subscribe now rather than on first read, so no signal published in between is missed.
        _subscribers.TryAdd(channel, 0);
        cancellationToken.Register(() =>
        {
            _subscribers.TryRemove(channel, out _);
            channel.Writer.TryComplete();
        });

        return channel.Reader.ReadAllAsync();
    }
}
