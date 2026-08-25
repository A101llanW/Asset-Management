using System;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork, IUnitOfWorkEnlistment
    {
        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly IOrganizationScopeService _organizationScope;
        private SqlConnection _connection;
        private UnitOfWorkSession _session;
        private bool _disposed;

        public UnitOfWork(ISqlConnectionFactory connectionFactory, IOrganizationScopeService organizationScope)
        {
            _connectionFactory = connectionFactory;
            _organizationScope = organizationScope;
        }

        public IRepository<T> Repository<T>() where T : class, new()
        {
            EnsureSession();
            return _session.GetRepository<T>();
        }

        public IEntityWriter<T> Writer<T>() where T : class, new()
        {
            EnsureSession();
            return _session.GetRepository<T>();
        }

        public int SaveChanges()
        {
            EnsureSession();
            return _session.SaveChanges();
        }

        public void BeginTransaction()
        {
            EnsureSession();
            _session.BeginTransaction();
        }

        public void Commit()
        {
            if (_session != null)
            {
                _session.Commit();
            }
        }

        public void Rollback()
        {
            if (_session != null)
            {
                _session.Rollback();
            }
        }

        public void ExecuteInTransaction(Action action)
        {
            EnsureSession();
            _session.ExecuteInTransaction(action);
        }

        public bool TryGetActiveTransaction(out IDbConnection connection, out IDbTransaction transaction)
        {
            connection = null;
            transaction = null;
            if (_session == null || !_session.HasActiveTransaction)
            {
                return false;
            }

            connection = _session.Connection;
            transaction = _session.Transaction;
            return true;
        }

        public void TrackAdd(object entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            EnsureSession();
            _session.Track(entity, TrackedEntityState.Added);
        }

        public int GetRemainingPurchaseQuantity(int purchaseRecordId)
        {
            EnsureSession();
            return EntitySqlWriter.GetRemainingPurchaseQuantity(_session.Connection, _session.Transaction, purchaseRecordId);
        }

        public void PersistConditionalApprovalUpdate(object entity, int expectedStage)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            EnsureSession();
            var map = EntityMapRegistry.GetMap(entity.GetType());
            var writeOptions = new SqlWriteOptions
            {
                Transaction = _session.Transaction,
                RequireApprovalStatus = (int)ApprovalStatus.Pending,
                RequireApprovalStage = expectedStage
            };
            EntitySqlWriter.Update(_session.Connection, map, entity, writeOptions);
            _session.Track(entity, TrackedEntityState.Unchanged);
        }

        public void ClearTracking()
        {
            EnsureSession();
            _session.ClearTracking();
        }

        private void EnsureSession()
        {
            if (_session != null)
            {
                return;
            }

            _connection = _connectionFactory.CreateConnection();
            _connection.Open();
            _session = new UnitOfWorkSession(_connection, _organizationScope);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            _session = null;
            _disposed = true;
        }
    }
}
