namespace Ratatosk.Application.Commands;

public record AddProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price
);
