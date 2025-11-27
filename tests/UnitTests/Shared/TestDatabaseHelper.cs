using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Ratatosk.UnitTests.Shared;

public static class TestDatabaseHelper
{
    public static async Task InitializeSchemaAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText =
            @"
            IF OBJECT_ID('EventStore', 'U') IS NULL
            CREATE TABLE EventStore (
                EventId UNIQUEIDENTIFIER PRIMARY KEY,
                StreamName NVARCHAR(200) NOT NULL,
                AggregateId UNIQUEIDENTIFIER NOT NULL,
                EventType NVARCHAR(400) NOT NULL,
                EventData NVARCHAR(MAX) NOT NULL,
                CreatedAt DATETIME2 NOT NULL,
                Version INT NOT NULL
            );

            IF OBJECT_ID('Snapshots', 'U') IS NULL
            CREATE TABLE Snapshots (
                AggregateId UNIQUEIDENTIFIER PRIMARY KEY,
                Version INT NOT NULL,
                AggregateType NVARCHAR(400) NOT NULL,
                Payload NVARCHAR(MAX) NOT NULL,
                Timestamp DATETIME2 NOT NULL
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task ClearTablesAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM EventStore; DELETE FROM Snapshots;";
        await cmd.ExecuteNonQueryAsync();
    }

    public static IDbConnection CreateInMemoryConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Optionally create tables here for testing
        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            CREATE TABLE EventStore (
                EventId TEXT PRIMARY KEY,
                AggregateId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                EventData TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Version INTEGER NOT NULL
            );

            CREATE TABLE Snapshots (
                AggregateId TEXT PRIMARY KEY,
                Version INTEGER NOT NULL,
                AggregateType TEXT NOT NULL,
                Payload TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
        ";
        command.ExecuteNonQuery();

        return connection;
    }
}
