using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Ordering.Queries;

/// <summary>
/// A customer's own orders, newest first, optionally only those in one status. The customer id
/// comes from the caller's identity, never from the client.
/// </summary>
public sealed record GetCustomerOrdersQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 25,
    string? Status = null
) : IRequest<Result<Pagination<OrderReadModel>>>;

public class GetCustomerOrdersQueryHandler(IOrderReadModelRepository repository)
    : IRequestHandler<GetCustomerOrdersQuery, Result<Pagination<OrderReadModel>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<Pagination<OrderReadModel>>> HandleAsync(
        GetCustomerOrdersQuery request,
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
