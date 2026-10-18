using Dapper;
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
                SELECT id, customer_id, status, created_utc, last_updated_utc, cancellation_reason
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

    public async Task<Pagination<OrderReadModel>> GetPageAsync(
        Guid? customerId = null,
        string? status = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    )
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (customerId is not null)
        {
            conditions.Add("customer_id = @CustomerId");
            parameters.Add("CustomerId", customerId);
        }

        if (status is not null)
        {
            conditions.Add("status = @Status");
            parameters.Add("Status", status);
        }

        var orders = await PagedQueryAsync<OrderReadModel>(
            fromAndJoinsSql: "FROM order_read_models",
            selectColumnsSql:
                "SELECT id, customer_id, status, created_utc, last_updated_utc, cancellation_reason",
            whereSql: conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : null,
            parameters: parameters,
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
                INSERT INTO order_read_models (id, customer_id, status, created_utc, last_updated_utc, cancellation_reason)
                VALUES (@Id, @CustomerId, @Status, @CreatedUtc, @LastUpdatedUtc, @CancellationReason)
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    last_updated_utc = EXCLUDED.last_updated_utc,
                    cancellation_reason = EXCLUDED.cancellation_reason
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
