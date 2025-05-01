namespace Ratatosk.Application.Commands;

public sealed record AddProductCommand(
    string Name,
    string Sku,
    string Description,
    decimal Price
);
