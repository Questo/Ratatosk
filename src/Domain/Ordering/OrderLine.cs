using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Catalog.ValueObjects;

namespace Ratatosk.Domain.Ordering;

public sealed class OrderLine : ValueObject
{
    public SKU Sku { get; }
    public int Quantity { get; }
    public Price UnitPrice { get; }

    private OrderLine(SKU sku, int quantity, Price unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public static Result<OrderLine> Create(SKU sku, int quantity, Price unitPrice)
    {
        Guard.AgainstNull(sku, nameof(sku));
        Guard.AgainstNull(unitPrice, nameof(unitPrice));

        if (quantity <= 0)
            return Result<OrderLine>.Failure("Quantity must be greater than zero");

        return Result<OrderLine>.Success(new OrderLine(sku, quantity, unitPrice));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Sku;
        yield return Quantity;
        yield return UnitPrice;
    }
}
