using Ratatosk.Application.Ordering;
using Ratatosk.Application.Ordering.Models;
using Ratatosk.Application.Shared;
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
}
