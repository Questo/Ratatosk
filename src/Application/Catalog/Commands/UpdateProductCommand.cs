namespace Ratatosk.Application.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal? Price
);
