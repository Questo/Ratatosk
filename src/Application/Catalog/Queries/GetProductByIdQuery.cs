using Ratatosk.Application.Catalog.ReadModels;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Catalog.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductReadModel>;

public class GetProductByIdQueryHandler(IProductReadModelRepository repository) : IHandler<GetProductByIdQuery, ProductReadModel>
{
    public async Task<Result<ProductReadModel>> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await repository.GetByIdAsync(query.Id, cancellationToken);
            if (product == null)
            {
                return Result<ProductReadModel>.Failure("Product not found");
            }

            return Result<ProductReadModel>.Success(product);
        }
        catch (Exception ex)
        {
            return Result<ProductReadModel>.Failure(Error.FromException(ex).Message);
        }
    }
}