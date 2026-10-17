using Ratatosk.Application.Inventoring;
using Ratatosk.Core.Abstractions;
using Ratatosk.Domain.Ordering;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Application.Ordering;

/// <summary>
/// Takes a placed Order to its outcome. It reserves stock line by line, then confirms the Order
/// once every line is reserved, or cancels it at the first line that cannot be reserved.
/// Whenever an Order is cancelled, whatever stock it holds is released (compensation).
/// </summary>
public sealed class OrderReservationProcess(IAggregateRepository<Order> orders, StockKeeper stock)
    : IDomainEventHandler<OrderCreated>,
        IDomainEventHandler<OrderCancelled>
{
    public const string UnexpectedFailureReason = "Stock reservation could not be completed";

    public async Task WhenAsync(OrderCreated domainEvent, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(domainEvent.OrderId, cancellationToken);
        if (order.Status != OrderStatus.Created)
            return;

        try
        {
            foreach (var line in order.Lines)
            {
                var reservation = await stock.ReserveForOrderAsync(
                    line.Sku,
                    line.Quantity,
                    order.Id,
                    cancellationToken
                );

                if (reservation.IsFailure)
                {
                    // Stop here: later lines would only be reserved to be released again.
                    order.Cancel(reservation.Error!).EnsureSuccess();
                    break;
                }

                order.MarkLineReserved(line.Sku).EnsureSuccess();
            }

            await orders.CommitAsync(order, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Never leave an Order stuck in Created while it may hold stock: cancel it, which
            // releases anything already reserved, then let the failure surface.
            var fresh = await LoadAsync(domainEvent.OrderId, cancellationToken);
            if (fresh.Cancel(UnexpectedFailureReason).IsSuccess)
                await orders.CommitAsync(fresh, cancellationToken);

            throw;
        }
    }

    public async Task WhenAsync(OrderCancelled domainEvent, CancellationToken cancellationToken = default)
    {
        foreach (var line in domainEvent.Lines)
            await stock.ReleaseForOrderAsync(line.Sku, domainEvent.OrderId, cancellationToken);
    }

    private async Task<Order> LoadAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await orders.LoadAsync(orderId, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error);

        return result.Value!;
    }
}
