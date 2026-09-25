using System.Data;
using Ratatosk.Application.Shared;

namespace Ratatosk.Infrastructure.Persistence;

public class UnitOfWork(string ConnectionString) : IDisposable, IUnitOfWork
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;

    public IDbConnection Connection =>
        _connection ?? throw new InvalidOperationException("Connection not initialized.");

    public IDbTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("Transaction not started.");

    public void Begin()
    {
        if (_connection != null)
        {
            throw new InvalidOperationException("Unit of Work has already been started.");
        }

        _connection = new Npgsql.NpgsqlConnection(ConnectionString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();
    }

    // Commits, then immediately opens a fresh transaction on the same connection so callers
    // (including multiple handlers sharing one scoped IUnitOfWork) can keep using it afterward,
    // and so calling Commit() more than once for the same logical operation is always safe.
    public void Commit()
    {
        try
        {
            _transaction!.Commit();
        }
        catch (Exception ex)
        {
            try
            {
                Rollback();
            }
            catch
            {
                // The original commit failure is the one worth surfacing; a rollback that also
                // fails (e.g. the transaction already completed) would otherwise mask it.
            }

            throw new InvalidOperationException("Failed to commit the transaction.", ex);
        }

        _transaction = _connection!.BeginTransaction();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();

        GC.SuppressFinalize(this);
    }

    public void Rollback()
    {
        if (_transaction == null)
        {
            return;
        }

        _transaction.Rollback();
    }
}
