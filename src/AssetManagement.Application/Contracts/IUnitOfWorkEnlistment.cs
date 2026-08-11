using System.Data;

namespace AssetManagement.Application.Contracts
{
    public interface IUnitOfWorkEnlistment
    {
        bool TryGetActiveTransaction(out IDbConnection connection, out IDbTransaction transaction);
    }
}
