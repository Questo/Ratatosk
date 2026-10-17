using Ratatosk.Domain.Catalog;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Services;

public class ProductDomainService(IUnitOfWork uow) : PostgresRepository(uow), IProductDomainService
{
    public async Task<bool> IsSkuUniqueAsync(
        string sku,
        CancellationToken cancellationToken = default
    )
    {
        var count = await ScalarAsync<int>(
            """
                SELECT COUNT(1)
                FROM product_read_models
                WHERE sku = @Sku
            """,
            new { Sku = sku },
            cancellationToken
        );

        return count == 0;
    }
}
