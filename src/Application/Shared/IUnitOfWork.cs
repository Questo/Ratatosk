using System.Data;

namespace Ratatosk.Application.Shared;

public interface IUnitOfWork
{
    public IDbConnection Connection { get; }
    public IDbTransaction Transaction { get; }

    void Begin();
    void Commit();
    void Dispose();
    void Rollback();
}
