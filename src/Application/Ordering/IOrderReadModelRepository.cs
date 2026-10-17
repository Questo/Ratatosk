using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;

namespace Ratatosk.Application.Ordering;

public interface IOrderReadModelRepository
{
    Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The customer's orders, newest first (by placement time), with their lines.</summary>
    Task<Pagination<OrderReadModel>> GetByCustomerAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    );

    Task SaveAsync(OrderReadModel order, CancellationToken cancellationToken = default);
}
