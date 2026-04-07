using System;

namespace AssetManagement.Application.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class;

        int SaveChanges();
    }
}
