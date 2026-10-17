using Microsoft.AspNetCore.Mvc;

namespace Ratatosk.API.Products;

public record SearchProductsRequest(
    [FromQuery] string? SearchTerm,
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 25,
    [FromQuery] decimal? MinPrice = null,
    [FromQuery] decimal? MaxPrice = null
);
