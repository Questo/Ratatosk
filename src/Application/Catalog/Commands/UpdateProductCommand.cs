using Ratatosk.Core.Abstractions;

namespace Ratatosk.Application.Catalog.Commands;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal? Price
) : ICommand;
