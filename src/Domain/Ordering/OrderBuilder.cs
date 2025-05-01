using Ratatosk.Core.Abstractions;

namespace Ratatosk.Domain.Ordering;

public class OrderBuilder : IBuilder<Order>
{
    public Order Build()
    {
        throw new NotImplementedException();
    }

    public IBuilder<Order> With<TValue>(string propertyName, TValue value)
    {
        throw new NotImplementedException();
    }
}