namespace Ratatosk.Application.Commands;

public record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal? Price
);
