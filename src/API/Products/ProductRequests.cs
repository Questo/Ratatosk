using Microsoft.AspNetCore.Mvc;

namespace Ratatosk.API.Products;

public record CreateProductRequest(string Name, decimal Price);

public record SearchProductsRequest(
    [FromQuery] string? SearchTerm,
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 25);
