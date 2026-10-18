using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering.Queries;

public sealed record GetOrderHistoryQuery(Guid OrderId) : IRequest<Result<IReadOnlyList<OrderHistoryEntry>>>;

/// <summary>
/// Replays the Order's stream one event at a time and records the Order after each one. The
/// one Order read that comes from the events rather than the read model, which only holds the
/// latest state.
/// </summary>
public class GetOrderHistoryQueryHandler(IAggregateRepository<Order> orders)
    : IRequestHandler<GetOrderHistoryQuery, Result<IReadOnlyList<OrderHistoryEntry>>>
{
    public async Task<Result<IReadOnlyList<OrderHistoryEntry>>> HandleAsync(
        GetOrderHistoryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var history = await orders.LoadHistoryAsync(query.OrderId, cancellationToken);
        if (history.IsFailure)
            return Result<IReadOnlyList<OrderHistoryEntry>>.Failure($"No order found with id {query.OrderId}");

        var order = new Order();
        var entries = new List<OrderHistoryEntry>();
        foreach (var domainEvent in history.Value!)
        {
            order.LoadFromHistory([domainEvent]);
            entries.Add(
                new OrderHistoryEntry(
                    order.Version,
                    domainEvent.GetType().Name,
                    domainEvent.OccurredAtUtc,
                    OrderProjection.ToReadModel(order),
                    [.. order.ReservedSkus.Select(s => s.Value)]
                )
            );
        }

        return Result<IReadOnlyList<OrderHistoryEntry>>.Success(entries);
    }
}
