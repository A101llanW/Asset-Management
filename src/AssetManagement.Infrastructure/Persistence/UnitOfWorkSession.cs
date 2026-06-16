using System;

using System.Collections.Generic;

using System.Data.SqlClient;

using System.Linq;

using System.Reflection;

using AssetManagement.Application.Contracts.Security;

using AssetManagement.Application.DTOs;

using AssetManagement.Domain.Common;

using AssetManagement.Infrastructure.Identity;

using AssetManagement.Infrastructure.Repositories;



namespace AssetManagement.Infrastructure.Persistence

{

    public enum TrackedEntityState

    {

        Unchanged,

        Added,

        Modified,

        Deleted

    }



    public sealed class TrackedEntity

    {

        public object Entity { get; set; }



        public TrackedEntityState State { get; set; }

    }



    public sealed class UnitOfWorkSession

    {

        private readonly SqlConnection _connection;

        private readonly IOrganizationScopeService _organizationScope;

        private readonly Dictionary<Type, IList<TrackedEntity>> _trackedByType = new Dictionary<Type, IList<TrackedEntity>>();

        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();

        private SqlTransaction _transaction;



        public UnitOfWorkSession(SqlConnection connection, IOrganizationScopeService organizationScope)

        {

            _connection = connection;

            _organizationScope = organizationScope;

        }



        public SqlConnection Connection

        {

            get { return _connection; }

        }



        public SqlTransaction Transaction

        {

            get { return _transaction; }

        }



        public bool HasActiveTransaction

        {

            get { return _transaction != null; }

        }



        public AdoRepository<T> GetRepository<T>() where T : class, new()

        {

            var entityType = typeof(T);

            object repository;

            if (!_repositories.TryGetValue(entityType, out repository))

            {

                repository = new AdoRepository<T>(this, _organizationScope);

                _repositories[entityType] = repository;

            }



            return (AdoRepository<T>)repository;

        }



        public IList<TrackedEntity> GetTrackedEntities(Type entityType)

        {

            IList<TrackedEntity> tracked;

            if (!_trackedByType.TryGetValue(entityType, out tracked))

            {

                tracked = new List<TrackedEntity>();

                _trackedByType[entityType] = tracked;

            }



            return tracked;

        }



        public IEnumerable<KeyValuePair<Type, IList<TrackedEntity>>> GetAllTracked()

        {

            return _trackedByType;

        }



        public void Track(object entity, TrackedEntityState state)

        {

            if (entity == null)

            {

                return;

            }



            var entityType = entity.GetType();

            var tracked = GetTrackedEntities(entityType);

            var existing = tracked.FirstOrDefault(x => ReferenceEquals(x.Entity, entity));

            if (existing == null)

            {

                tracked.Add(new TrackedEntity { Entity = entity, State = state });

                return;

            }



            if (existing.State == TrackedEntityState.Unchanged && state == TrackedEntityState.Modified)

            {

                existing.State = TrackedEntityState.Modified;

            }

            else if (existing.State == TrackedEntityState.Added && state == TrackedEntityState.Deleted)

            {

                tracked.Remove(existing);

            }

            else if (state == TrackedEntityState.Deleted)

            {

                existing.State = TrackedEntityState.Deleted;

            }

        }



        public void HydrateNavigations()

        {

            foreach (var pair in _trackedByType)

            {

                var map = EntityMapRegistry.GetMap(pair.Key);

                foreach (var tracked in pair.Value.Where(x => x.State != TrackedEntityState.Deleted))

                {

                    foreach (var navigation in map.Navigations)

                    {

                        var foreignKeyValue = navigation.NavigationProperty.ReflectedType

                            .GetProperty(navigation.ForeignKeyProperty)

                            .GetValue(tracked.Entity, null);

                        if (foreignKeyValue == null)

                        {

                            navigation.NavigationProperty.SetValue(tracked.Entity, null, null);

                            continue;

                        }



                        var relatedRepository = GetRelatedEntities(navigation.RelatedEntityType);

                        var primaryKey = EntityMapRegistry.GetMap(navigation.RelatedEntityType).PrimaryKey;

                        var relatedEntity = relatedRepository.FirstOrDefault(related =>

                        {

                            var keyValue = navigation.RelatedEntityType.GetProperty(primaryKey).GetValue(related, null);

                            return Equals(keyValue, foreignKeyValue);

                        });



                        navigation.NavigationProperty.SetValue(tracked.Entity, relatedEntity, null);

                    }

                }

            }

        }



        private IEnumerable<object> GetRelatedEntities(Type entityType)

        {

            IList<TrackedEntity> tracked;

            if (!_trackedByType.TryGetValue(entityType, out tracked))

            {

                yield break;

            }



            foreach (var item in tracked.Where(x => x.State != TrackedEntityState.Deleted))

            {

                yield return item.Entity;

            }

        }



        public void BeginTransaction()

        {

            if (_transaction != null)

            {

                throw new InvalidOperationException("A transaction is already active on this unit of work.");

            }



            _transaction = _connection.BeginTransaction();

        }



        public void Commit()

        {

            if (_transaction == null)

            {

                return;

            }



            _transaction.Commit();

            _transaction.Dispose();

            _transaction = null;

        }



        public void Rollback()

        {

            if (_transaction == null)

            {

                return;

            }



            _transaction.Rollback();

            _transaction.Dispose();

            _transaction = null;

        }



        public void ExecuteInTransaction(Action action)

        {

            if (action == null)

            {

                throw new ArgumentNullException("action");

            }



            var ownsTransaction = _transaction == null;

            if (ownsTransaction)

            {

                BeginTransaction();

            }



            try

            {

                action();

                SaveChanges();

                if (ownsTransaction)

                {

                    Commit();

                }

            }

            catch

            {

                if (ownsTransaction)

                {

                    Rollback();

                }



                throw;

            }

        }



        public int SaveChanges()

        {

            var utcNow = DateTime.UtcNow;

            var total = 0;

            var writeOptions = new SqlWriteOptions { Transaction = _transaction };



            foreach (var pair in _trackedByType.ToList())

            {

                var map = EntityMapRegistry.GetMap(pair.Key);

                foreach (var tracked in pair.Value.ToList())

                {

                    ApplyAuditFields(tracked.Entity, tracked.State, utcNow);

                    switch (tracked.State)

                    {

                        case TrackedEntityState.Added:

                            EntitySqlWriter.Insert(_connection, map, tracked.Entity, writeOptions);

                            tracked.State = TrackedEntityState.Unchanged;

                            total++;

                            break;

                        case TrackedEntityState.Modified:

                            EntitySqlWriter.Update(_connection, map, tracked.Entity, writeOptions);

                            tracked.State = TrackedEntityState.Unchanged;

                            total++;

                            break;

                        case TrackedEntityState.Deleted:

                            EntitySqlWriter.Delete(_connection, map, tracked.Entity, writeOptions);

                            pair.Value.Remove(tracked);

                            total++;

                            break;

                    }

                }

            }



            HydrateNavigations();

            return total;

        }



        private void ApplyAuditFields(object entity, TrackedEntityState state, DateTime utcNow)

        {

            var auditable = entity as AuditableEntity;

            var tenantEntity = entity as ITenantEntity;

            if (tenantEntity != null && state == TrackedEntityState.Added && !tenantEntity.OrganizationId.HasValue && _organizationScope != null)

            {

                var orgId = _organizationScope.GetCurrentOrganizationId();

                if (orgId.HasValue)

                {

                    tenantEntity.OrganizationId = orgId;

                }

            }



            if (auditable != null)

            {

                if (state == TrackedEntityState.Added)

                {

                    auditable.CreatedAt = utcNow;

                    auditable.IsActive = true;

                }

                else if (state == TrackedEntityState.Modified)

                {

                    auditable.UpdatedAt = utcNow;

                }



                return;

            }



            var user = entity as ApplicationUser;

            if (user != null)

            {

                if (state == TrackedEntityState.Added)

                {

                    user.CreatedAt = utcNow;

                    user.IsActive = true;

                    if (!user.OrganizationId.HasValue && _organizationScope != null)

                    {

                        var orgId = _organizationScope.GetCurrentOrganizationId();

                        if (orgId.HasValue)

                        {

                            user.OrganizationId = orgId;

                        }

                    }

                }

                else if (state == TrackedEntityState.Modified)

                {

                    user.UpdatedAt = utcNow;

                }

            }

        }

    }

}


