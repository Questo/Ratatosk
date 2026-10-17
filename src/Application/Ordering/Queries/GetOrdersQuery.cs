using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering.Queries;

/// <summary>
/// Every customer's orders, newest first, optionally only one customer's and only those in one
/// status. An Admin capability: the endpoint dispatches it only for Admins.
/// </summary>
public sealed record GetOrdersQuery(
    Guid? CustomerId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<Result<Pagination<OrderReadModel>>>;

public class GetOrdersQueryHandler(IOrderReadModelRepository repository)
    : IRequestHandler<GetOrdersQuery, Result<Pagination<OrderReadModel>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<Pagination<OrderReadModel>>> HandleAsync(
        GetOrdersQuery request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.Page < 1)
            return Result<Pagination<OrderReadModel>>.Failure("Page must be at least 1.");

        if (request.PageSize is < 1 or > MaxPageSize)
            return Result<Pagination<OrderReadModel>>.Failure(
                $"Page size must be between 1 and {MaxPageSize}."
            );

        var status = OrderStatusFilter.Resolve(request.Status);
        if (status.IsFailure)
            return Result<Pagination<OrderReadModel>>.Failure(status.Error!);

        var orders = await repository.GetPageAsync(
            request.CustomerId,
            status.Value,
            request.Page,
            request.PageSize,
            cancellationToken
        );
        return Result<Pagination<OrderReadModel>>.Success(orders);
    }
}
