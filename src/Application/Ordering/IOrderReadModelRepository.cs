using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;

namespace Ratatosk.Application.Ordering;

public interface IOrderReadModelRepository
{
    Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orders newest first (by placement time), with their lines: all of them, or only those of
    /// <paramref name="customerId"/> and/or in <paramref name="status"/> (a canonical
    /// <c>OrderStatus</c> name) when given.
    /// </summary>
    Task<Pagination<OrderReadModel>> GetPageAsync(
        Guid? customerId = null,
        string? status = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// At most <paramref name="limit"/> Orders, newest first, with their lines, whose id, customer
    /// id, status or the SKU of any line contains <paramref name="term"/>, ignoring case.
    /// </summary>
    Task<IReadOnlyList<OrderReadModel>> SearchAsync(
        string term,
        int limit,
        CancellationToken cancellationToken = default
    );

    Task SaveAsync(OrderReadModel order, CancellationToken cancellationToken = default);
}
