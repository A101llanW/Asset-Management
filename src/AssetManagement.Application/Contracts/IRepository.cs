using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AssetManagement.Application.Contracts
{
    public interface IRepository<T> where T : class
    {
        IEnumerable<T> GetAll();

        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);

        T GetById(object id);

        void Add(T entity);

        void Update(T entity);

        void Remove(T entity);
    }
}
