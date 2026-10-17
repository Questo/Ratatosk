using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Ratatosk.Infrastructure.Configuration;

namespace Ratatosk.Infrastructure.Persistence;

/// <summary>
/// Brings a database created by an older postgres/init/01-init.sql up to the current schema.
/// The init script only runs on an empty volume; this runs on every start and changes nothing
/// once the schema is current.
/// </summary>
public static class SchemaUpgrade
{
    private const string Sql = """
        -- Snapshots are keyed by aggregate type too: an Inventory shares its id with its Product.
        DO $$
        DECLARE
            key_name text;
            key_columns int;
        BEGIN
            SELECT conname, array_length(conkey, 1) INTO key_name, key_columns
            FROM pg_constraint
            WHERE conrelid = to_regclass('snapshots') AND contype = 'p';

            IF key_columns = 1 THEN
                EXECUTE format('ALTER TABLE snapshots DROP CONSTRAINT %I', key_name);
                ALTER TABLE snapshots ADD PRIMARY KEY (aggregate_type, aggregate_id);
            END IF;
        END $$;

        -- The stock record's "available" was always the amount on hand; available is now derived.
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'inventory_stock_read_models'
                  AND column_name = 'available'
            ) THEN
                ALTER TABLE inventory_stock_read_models RENAME COLUMN available TO on_hand;
            END IF;
        END $$;

        ALTER TABLE IF EXISTS inventory_stock_read_models
            ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 0;

        -- The user id, carried in the token. Null for users created before it was recorded.
        ALTER TABLE IF EXISTS user_auth_read_models ADD COLUMN IF NOT EXISTS id uuid;

        -- The Order status Created is now called Placed.
        DO $$
        BEGIN
            IF to_regclass('order_read_models') IS NOT NULL THEN
                UPDATE order_read_models SET status = 'Placed' WHERE status = 'Created';
            END IF;
        END $$;
        """;

    public static Task ApplyAsync(
        IDbConnection connection,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) =>
        connection.ExecuteAsync(
            new CommandDefinition(Sql, transaction: transaction, cancellationToken: cancellationToken)
        );

    public static async Task UpgradeDatabaseSchemaAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await ApplyAsync(connection, cancellationToken: cancellationToken);
    }
}
