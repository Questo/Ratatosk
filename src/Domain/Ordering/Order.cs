using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.ValueObjects;
using Ratatosk.Domain.Ordering.Events;

namespace Ratatosk.Domain.Ordering;

public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    private readonly HashSet<SKU> _reservedSkus = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderCreated e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                _lines.AddRange(e.Lines);
                Status = OrderStatus.Created;
                break;

            case OrderLineReserved e:
                _reservedSkus.Add(e.Sku);
                break;

            case OrderConfirmed:
                Status = OrderStatus.Confirmed;
                break;

            case OrderCancelled:
                Status = OrderStatus.Cancelled;
                break;
        }
    }

    public static Result<Order> Place(Guid customerId, IEnumerable<OrderLine> lines)
    {
        try
        {
            Guard.AgainstEmpty(customerId, nameof(customerId));

            var lineList = lines?.ToList() ?? [];
            if (lineList.Count == 0)
                return Result<Order>.Failure("Order must contain at least one line");

            if (lineList.Select(l => l.Sku).Distinct().Count() != lineList.Count)
                return Result<Order>.Failure("Order cannot contain duplicate SKUs");

            var order = new Order();
            order.RaiseEvent(new OrderCreated(order.Id, customerId, lineList));

            return Result<Order>.Success(order);
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result<Order>.Failure(error.Message);
        }
    }

    public Result MarkLineReserved(SKU sku)
    {
        if (Status != OrderStatus.Created)
            return Result.Failure($"Cannot reserve a line for an order in status {Status}");

        if (!_lines.Any(l => l.Sku.Equals(sku)))
            return Result.Failure($"SKU {sku} is not part of this order");

        if (_reservedSkus.Contains(sku))
            return Result.Failure($"SKU {sku} was already marked as reserved");

        RaiseEvent(new OrderLineReserved(Id, sku));

        if (_lines.Select(l => l.Sku).All(_reservedSkus.Contains))
            RaiseEvent(new OrderConfirmed(Id));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        try
        {
            Guard.AgainstNullOrEmpty(reason, nameof(reason));

            if (Status != OrderStatus.Created)
                return Result.Failure($"Cannot cancel an order in status {Status}");

            RaiseEvent(new OrderCancelled(Id, reason, _lines));

            return Result.Success();
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result.Failure(error.Message);
        }
    }
}
