using System;

namespace AssetManagement.Application.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<T> Repository<T>() where T : class, new();

        IEntityWriter<T> Writer<T>() where T : class, new();

        int SaveChanges();

        void BeginTransaction();

        void Commit();

        void Rollback();

        void ExecuteInTransaction(Action action);

        void TrackAdd(object entity);

        void PersistConditionalApprovalUpdate(object entity, int expectedStage);

        int GetRemainingPurchaseQuantity(int purchaseRecordId);
    }
}
