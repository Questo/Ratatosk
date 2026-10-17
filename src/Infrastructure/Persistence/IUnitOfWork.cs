using System.Data;

namespace Ratatosk.Infrastructure.Persistence;

/// <summary>
/// The scope's database transaction. Owned by Infrastructure: aggregate commits, event handler
/// dispatch and the per-request endpoint filter are the only places that end it.
/// </summary>
public interface IUnitOfWork
{
    public IDbConnection Connection { get; }
    public IDbTransaction Transaction { get; }

    void Begin();

    /// <summary>Commits, then starts a fresh transaction on the same connection.</summary>
    void Commit();

    /// <summary>Rolls back, then starts a fresh transaction on the same connection.</summary>
    void Rollback();

    void Dispose();
}
