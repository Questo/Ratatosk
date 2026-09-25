using Ratatosk.Application.Ordering.Models;

namespace Ratatosk.Application.Ordering;

public interface IOrderReadModelRepository
{
    Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(OrderReadModel order, CancellationToken cancellationToken = default);
}
