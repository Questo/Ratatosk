using Microsoft.AspNetCore.Mvc;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Commands;

namespace Ratatosk.API.Products;

[ApiController]
[Route("products")]
public class ProductsController(ICatalogService catalogService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] AddProductCommand cmd, CancellationToken ct)
    {
        var result = await catalogService.AddProductAsync(cmd, ct);

        return result.IsFailure
            ? BadRequest(result.Error)
            : Created();
    }
}
