using Ratatosk.Application.Ordering.Models;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering.Queries;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderReadModel>>;

public class GetOrderByIdQueryHandler(IOrderReadModelRepository repository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderReadModel>>
{
    public async Task<Result<OrderReadModel>> HandleAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var order = await repository.GetByIdAsync(query.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderReadModel>.Failure($"No order found with id {query.OrderId}");

        return Result<OrderReadModel>.Success(order);
    }
}
