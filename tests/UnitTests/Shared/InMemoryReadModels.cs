using System.Collections.Concurrent;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Models;
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
        decimal? minPrice = null,
        decimal? maxPrice = null,
        CancellationToken cancellationToken = default
    )
    {
        var matches = _rows
            .Values.Where(r =>
                (searchTerm is null || r.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                && (minPrice is null || r.Price >= minPrice)
                && (maxPrice is null || r.Price <= maxPrice)
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

    // Same contract as the Postgres upsert: never replace a record of the same or a newer version.
    public Task SaveAsync(StockReadModel stock, CancellationToken cancellationToken = default)
    {
        _rows.AddOrUpdate(
            stock.ProductId,
            _ => Copy(stock),
            (_, stored) => stored.Version < stock.Version ? Copy(stock) : stored
        );
        return Task.CompletedTask;
    }

    private static StockReadModel Copy(StockReadModel r) =>
        new(r.ProductId, r.Sku, r.OnHand, r.Reserved, r.Unit, r.Version, r.LastUpdatedUtc);
}

public sealed class InMemoryOrderReadModelRepository : IOrderReadModelRepository
{
    private readonly ConcurrentDictionary<Guid, OrderReadModel> _rows = new();

    public int SaveCount { get; private set; }

    public Task<OrderReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? Copy(row) : null);

    public Task<Pagination<OrderReadModel>> GetByCustomerAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var matches = _rows
            .Values.Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedUtc)
            .ThenByDescending(r => r.Id)
            .ToList();

        return Task.FromResult(
            new Pagination<OrderReadModel>
            {
                Items = [.. matches.Skip((page - 1) * pageSize).Take(pageSize).Select(Copy)],
                TotalItems = matches.Count,
                Page = page,
                PageSize = pageSize,
            }
        );
    }

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

// Records are immutable, so rows need no copying.
public sealed class InMemoryUserAuthRepository : IUserAuthRepository
{
    private readonly ConcurrentDictionary<string, UserAuth> _rows = new();

    public Task<Pagination<UserAuth>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Page([.. _rows.Values], 1, Math.Max(_rows.Count, 1)));

    public Task<Pagination<UserAuth>> GetAllByRole(
        string role,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            Page([.. _rows.Values.Where(u => u.Role == role).Skip((page - 1) * pageSize).Take(pageSize)], page, pageSize)
        );

    public Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.GetValueOrDefault(email));

    public Task SaveAsync(UserAuth userAuth, CancellationToken cancellationToken)
    {
        _rows[userAuth.Email] = userAuth;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string email, CancellationToken cancellationToken)
    {
        _rows.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    private static Pagination<UserAuth> Page(List<UserAuth> items, int page, int pageSize) =>
        new()
        {
            Items = items,
            TotalItems = items.Count,
            Page = page,
            PageSize = pageSize,
        };
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<string, RefreshToken> _rows = new();

    public IReadOnlyCollection<RefreshToken> All => [.. _rows.Values];

    public Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _rows[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.GetValueOrDefault(token));

    public Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        _rows.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}
