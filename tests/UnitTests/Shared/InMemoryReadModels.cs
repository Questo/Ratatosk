using System.Collections.Concurrent;
using Ratatosk.Application.Catalog;
using Ratatosk.Application.Catalog.Models;
using Ratatosk.Application.Inventoring;
using Ratatosk.Application.Inventoring.Models;
using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Domain.Catalog;

namespace Ratatosk.UnitTests.Shared;

// In-memory adapters for the read-model seams, standing in for Postgres in TestHost.
// Rows are copied in and out so callers can't mutate stored state without saving it.

public sealed class InMemoryProductReadModelRepository : IProductReadModelRepository, IProductDomainService
{
    private readonly ConcurrentDictionary<Guid, ProductReadModel> _rows = new();

    public Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? Copy(row) : null);

    public Task<Pagination<ProductReadModel>> GetAllAsync(
        string? searchTerm = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var matches = _rows
            .Values.Where(r =>
                searchTerm is null || r.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(r => r.LastUpdatedUtc)
            .ToList();

        return Task.FromResult(
            new Pagination<ProductReadModel>
            {
                Items = [.. matches.Skip((page - 1) * pageSize).Take(pageSize).Select(Copy)],
                TotalItems = matches.Count,
                Page = page,
                PageSize = pageSize,
            }
        );
    }

    public Task SaveAsync(ProductReadModel product, CancellationToken cancellationToken = default)
    {
        _rows[product.Id] = Copy(product);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _rows.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> IsSkuUniqueAsync(string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Values.All(r => r.Sku != sku));

    private static ProductReadModel Copy(ProductReadModel r) =>
        new(r.Id, r.Name, r.Sku, r.Description, r.Price, r.LastUpdatedUtc);
}

public sealed class InMemoryInventoryReadModelRepository : IInventoryReadModelRepository
{
    private readonly ConcurrentDictionary<Guid, StockReadModel> _rows = new();

    public Task<StockReadModel?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(_rows.TryGetValue(productId, out var row) ? Copy(row) : null);

    public Task<StockReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Values.Where(r => r.Sku == sku).Select(Copy).FirstOrDefault());

    public Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default)
    {
        _rows[stock.ProductId] = Copy(stock);
        return Task.CompletedTask;
    }

    private static StockReadModel Copy(StockReadModel r) =>
        new(r.ProductId, r.Sku, r.Available, r.Reserved, r.Unit, r.LastUpdatedUtc);
}

public sealed class InMemoryOrderReadModelRepository : IOrderReadModelRepository
{
    private readonly ConcurrentDictionary<Guid, OrderReadModel> _rows = new();

    public int SaveCount { get; private set; }

    public Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? Copy(row) : null);

    public Task SaveAsync(OrderReadModel order, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        _rows[order.Id] = Copy(order);
        return Task.CompletedTask;
    }

    private static OrderReadModel Copy(OrderReadModel r) =>
        new()
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            Status = r.Status,
            Lines = [.. r.Lines.Select(l => new OrderLineReadModel(l.Sku, l.Quantity, l.UnitPrice, l.Currency))],
            CreatedUtc = r.CreatedUtc,
            LastUpdatedUtc = r.LastUpdatedUtc,
        };
}
