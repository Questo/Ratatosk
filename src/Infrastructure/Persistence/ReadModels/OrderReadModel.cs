using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
using Ratatosk.Infrastructure.Persistence;
using Ratatosk.Infrastructure.Persistence.Repositories;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class OrderReadModelRepository(IUnitOfWork uow)
    : PostgresRepository(uow),
      IOrderReadModelRepository
{
    public async Task<OrderReadModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var order = await QueryFirstOrDefaultAsync<OrderReadModel>(
            """
                SELECT id, customer_id, status, created_utc, last_updated_utc
                FROM order_read_models
                WHERE id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

        if (order is null)
            return null;

        var lines = await QueryAsync<OrderLineReadModel>(
            """
                SELECT sku, quantity, unit_price, currency
                FROM order_line_read_models
                WHERE order_id = @Id
            """,
            new { Id = id },
            cancellationToken
        );

        order.Lines = [.. lines];
        return order;
    }

    public async Task<Pagination<OrderReadModel>> GetByCustomerAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var orders = await PagedQueryAsync<OrderReadModel>(
            fromAndJoinsSql: "FROM order_read_models",
            selectColumnsSql: "SELECT id, customer_id, status, created_utc, last_updated_utc",
            whereSql: "WHERE customer_id = @CustomerId",
            parameters: new { CustomerId = customerId },
            orderBySql: "ORDER BY created_utc DESC, id DESC",
            page: page,
            pageSize: pageSize,
            ct: cancellationToken
        );

        if (orders.Items.Count == 0)
            return orders;

        var lines = await QueryAsync<OrderLineRow>(
            """
                SELECT order_id, sku, quantity, unit_price, currency
                FROM order_line_read_models
                WHERE order_id = ANY(@Ids)
            """,
            new { Ids = orders.Items.Select(o => o.Id).ToArray() },
            cancellationToken
        );

        var linesByOrder = lines.ToLookup(l => l.OrderId);
        foreach (var order in orders.Items)
        {
            order.Lines =
            [
                .. linesByOrder[order.Id]
                    .Select(l => new OrderLineReadModel(l.Sku, l.Quantity, l.UnitPrice, l.Currency)),
            ];
        }

        return orders;
    }

    public async Task SaveAsync(
        OrderReadModel order,
        CancellationToken cancellationToken = default
    )
    {
        await ExecAsync(
            """
                INSERT INTO order_read_models (id, customer_id, status, created_utc, last_updated_utc)
                VALUES (@Id, @CustomerId, @Status, @CreatedUtc, @LastUpdatedUtc)
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    last_updated_utc = EXCLUDED.last_updated_utc
            """,
            order,
            cancellationToken
        );

        await ExecAsync(
            """
                DELETE FROM order_line_read_models
                WHERE order_id = @Id
            """,
            new { order.Id },
            cancellationToken
        );

        foreach (var line in order.Lines)
        {
            await ExecAsync(
                """
                    INSERT INTO order_line_read_models (order_id, sku, quantity, unit_price, currency)
                    VALUES (@OrderId, @Sku, @Quantity, @UnitPrice, @Currency)
                """,
                new
                {
                    OrderId = order.Id,
                    line.Sku,
                    line.Quantity,
                    line.UnitPrice,
                    line.Currency,
                },
                cancellationToken
            );
        }
    }

    private sealed record OrderLineRow(
        Guid OrderId,
        string Sku,
        int Quantity,
        decimal UnitPrice,
        string Currency
    );
}
