using Ratatosk.Core.BuildingBlocks;

namespace Ratatosk.Application.Abstractions;

public interface IEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}