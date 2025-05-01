using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Ordering;

public class Order : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;

    protected override void ApplyEvent(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderCreated e:
                Id = e.OrderId;
                Name = e.Name;
                break;

            case OrderRenamed e:
                Name = e.NewName;
                break;
        }
    }

    public static Result<Order> Create(Guid id, string name)
    {
        try
        {
            Guard.AgainstEmpty(id, nameof(id));
            Guard.AgainstNullOrEmpty(name, nameof(name));

            Order order = new();
            OrderCreated @event = new(id, name);

            order.RaiseEvent(@event);

            return Result<Order>.Success(order);
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result<Order>.Failure(error.Message);
        }
    }

    public Result Rename(string newName)
    {
        try
        {
            Guard.AgainstNullOrEmpty(newName, nameof(newName));

            if (newName == Name)
                return Result.Failure("Name hasn't changed");

            OrderRenamed @event = new(Id, Name, newName);
            RaiseEvent(@event);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var error = Error.FromException(ex);
            return Result.Failure(error.Message);
        }
    }
}
