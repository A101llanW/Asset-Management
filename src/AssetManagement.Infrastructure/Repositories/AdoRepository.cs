using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Common;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Repositories
{
    public class AdoRepository<T> : IRepository<T>, IEntityWriter<T> where T : class, new()
    {
        private readonly UnitOfWorkSession _session;
        private readonly IOrganizationScopeService _organizationScope;
        private bool _loaded;

        public AdoRepository(UnitOfWorkSession session, IOrganizationScopeService organizationScope)
        {
            _session = session;
            _organizationScope = organizationScope;
        }

        public IQueryable<T> Query()
        {
            EnsureLoaded();
            return ApplyTenantFilter(GetTrackedEntities().Select(x => (T)x.Entity).AsQueryable());
        }

        public IEnumerable<T> GetAll()
        {
            return Query().ToList();
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return Query().Where(predicate).ToList();
        }

        public T GetById(object id)
        {
            var map = EntityMapRegistry.GetMap<T>();
            var primaryKey = map.PrimaryKey;
            var pkProperty = typeof(T).GetProperty(primaryKey);

            var tracked = GetTrackedEntities().FirstOrDefault(x =>
                Equals(pkProperty.GetValue(x.Entity, null), id));
            if (tracked != null)
            {
                return ApplyTenantFilterSingle((T)tracked.Entity);
            }

            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
            var entity = EntitySqlReader.ReadById<T>(_session.Connection, map, id, organizationId, applyOrgFilter, _session.Transaction);
            if (entity == null)
            {
                return null;
            }

            _session.GetTrackedEntities(typeof(T)).Add(new TrackedEntity { Entity = entity, State = TrackedEntityState.Unchanged });
            return ApplyTenantFilterSingle(entity);
        }

        public void Add(T entity)
        {
            _session.Track(entity, TrackedEntityState.Added);
        }

        public void Update(T entity)
        {
            var existing = GetTrackedEntities().FirstOrDefault(x => ReferenceEquals(x.Entity, entity));
            if (existing == null)
            {
                _session.GetTrackedEntities(typeof(T)).Add(new TrackedEntity
                {
                    Entity = entity,
                    State = TrackedEntityState.Modified
                });
            }
            else
            {
                _session.Track(entity, TrackedEntityState.Modified);
            }
        }

        public void Remove(T entity)
        {
            _session.Track(entity, TrackedEntityState.Deleted);
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            BlockApplicationUserFullTableLoad();

            var map = EntityMapRegistry.GetMap<T>();
            var entities = EntitySqlReader.ReadAll<T>(_session.Connection, map, _session.Transaction);
            var tracked = _session.GetTrackedEntities(typeof(T));
            foreach (var entity in entities)
            {
                tracked.Add(new TrackedEntity { Entity = entity, State = TrackedEntityState.Unchanged });
            }

            _loaded = true;
        }

        private static void BlockApplicationUserFullTableLoad()
        {
            if (typeof(T) == typeof(ApplicationUser))
            {
                throw new NotSupportedException(
                    "ApplicationUser full-table reads are not supported. Use IUserAccountQueryRepository for list and display lookups.");
            }
        }

        private IQueryable<T> ApplyTenantFilter(IQueryable<T> query)
        {
            if (_organizationScope == null)
            {
                return query;
            }

            return _organizationScope.ApplyOrganizationFilter(query);
        }

        private T ApplyTenantFilterSingle(T entity)
        {
            if (entity == null || _organizationScope == null)
            {
                return entity;
            }

            var tenantEntity = entity as ITenantEntity;
            if (tenantEntity == null)
            {
                return entity;
            }

            var currentOrgId = _organizationScope.GetCurrentOrganizationId();
            if (!currentOrgId.HasValue)
            {
                return entity;
            }

            if (!tenantEntity.OrganizationId.HasValue || tenantEntity.OrganizationId.Value != currentOrgId.Value)
            {
                return null;
            }

            return entity;
        }

        private IEnumerable<TrackedEntity> GetTrackedEntities()
        {
            return _session.GetTrackedEntities(typeof(T));
        }
    }
}
