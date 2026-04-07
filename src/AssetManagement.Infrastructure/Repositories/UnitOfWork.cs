using System;
using System.Collections;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AssetManagementDbContext _context;
        private readonly Hashtable _repositories;

        public UnitOfWork(AssetManagementDbContext context)
        {
            _context = context;
            _repositories = new Hashtable();
        }

        public IRepository<T> Repository<T>() where T : class
        {
            var typeName = typeof(T).Name;
            if (!_repositories.ContainsKey(typeName))
            {
                var repository = new EfRepository<T>(_context);
                _repositories.Add(typeName, repository);
            }

            return (IRepository<T>)_repositories[typeName];
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
