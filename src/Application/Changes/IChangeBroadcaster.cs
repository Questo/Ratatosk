namespace Ratatosk.Application.Changes;

/// <summary>
/// Hands change signals to the clients currently subscribed (ADR 0002).
/// </summary>
public interface IChangeBroadcaster
{
    /// <summary>Hands the signal to every current subscriber, without waiting for any of them.</summary>
    void Publish(ChangeSignal signal);

    /// <summary>
    /// The signals published from now on. The stream ends, and the subscription is removed, when
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<ChangeSignal> SubscribeAsync(CancellationToken cancellationToken);
}
