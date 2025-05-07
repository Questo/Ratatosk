using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Core.Abstractions;

public interface IDomainEventHandler<TEvent> where TEvent : DomainEvent
{
    Task WhenAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}