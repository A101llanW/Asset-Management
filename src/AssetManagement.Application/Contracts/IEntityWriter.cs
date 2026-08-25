namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Write path for tracked entity mutations. Use instead of IRepository for Add/Update/Remove/GetById.
    /// </summary>
    public interface IEntityWriter<T> where T : class, new()
    {
        T GetById(object id);

        void Add(T entity);

        void Update(T entity);

        void Remove(T entity);
    }
}
