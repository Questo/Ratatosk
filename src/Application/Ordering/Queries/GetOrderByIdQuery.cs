using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering.Queries;

public sealed record GetOrderByIdQuery(Guid OrderId, Guid CallerId, bool CallerIsAdmin) : IRequest<Result<OrderReadModel>>;

public class GetOrderByIdQueryHandler(IOrderReadModelRepository repository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderReadModel>>
{
    public async Task<Result<OrderReadModel>> HandleAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var order = await repository.GetByIdAsync(query.OrderId, cancellationToken);
        // Another customer's order looks exactly like a missing one, so callers cannot probe order ids.
        if (order is null || (!query.CallerIsAdmin && order.CustomerId != query.CallerId))
            return Result<OrderReadModel>.Failure($"No order found with id {query.OrderId}");

        return Result<OrderReadModel>.Success(order);
    }
}
