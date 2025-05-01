namespace Ratatosk.Application.Catalog;

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    decimal Price);