namespace Ratatosk.API.Products;

public record ProductDto(Guid Id, string SKU, string Name, string Description, decimal Price);
