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
            return new SqlQueryable<T>(ExecuteQuery);
        }

        public IEnumerable<T> GetAll()
        {
            return Query().ToList();
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            var map = EntityMapRegistry.GetMap<T>();
            var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
            var tenantOrganizationId = _organizationScope == null || !applyOrgFilter
                ? null
                : _organizationScope.GetTenantFilterOrganizationId(typeof(T));
            var entities = EntitySqlReader.ReadWhere(
                _session.Connection,
                map,
                predicate,
                tenantOrganizationId,
                applyOrgFilter,
                _session.Transaction);
            var tracked = _session.GetTrackedEntities(typeof(T));
            foreach (var entity in entities)
            {
                var existing = tracked.FirstOrDefault(x =>
                    Equals(map.EntityType.GetProperty(map.PrimaryKey).GetValue(x.Entity, null),
                        map.EntityType.GetProperty(map.PrimaryKey).GetValue(entity, null)));
                if (existing == null)
                {
                    tracked.Add(new TrackedEntity { Entity = entity, State = TrackedEntityState.Unchanged });
                }
                else
                {
                    entities[entities.IndexOf(entity)] = (T)existing.Entity;
                }
            }

            return entities;
        }

        public int Count(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            var map = EntityMapRegistry.GetMap<T>();
            var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
            var tenantOrganizationId = _organizationScope == null || !applyOrgFilter
                ? null
                : _organizationScope.GetTenantFilterOrganizationId(typeof(T));
            return EntitySqlReader.CountWhere(
                _session.Connection,
                map,
                predicate,
                tenantOrganizationId,
                applyOrgFilter,
                _session.Transaction);
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

            var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
            var tenantOrganizationId = _organizationScope == null || !applyOrgFilter
                ? null
                : _organizationScope.GetTenantFilterOrganizationId(typeof(T));
            var entity = EntitySqlReader.ReadById<T>(_session.Connection, map, id, tenantOrganizationId, applyOrgFilter, _session.Transaction);
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

        internal void ResetQueryCache()
        {
            _loaded = false;
        }

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            BlockApplicationUserFullTableLoad();

            var map = EntityMapRegistry.GetMap<T>();
            var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
            var tenantOrganizationId = _organizationScope == null || !applyOrgFilter
                ? null
                : _organizationScope.GetTenantFilterOrganizationId(typeof(T));
            var entities = EntitySqlReader.ReadAll<T>(_session.Connection, map, tenantOrganizationId, applyOrgFilter, _session.Transaction);
            var tracked = _session.GetTrackedEntities(typeof(T));
            foreach (var entity in entities)
            {
                tracked.Add(new TrackedEntity { Entity = entity, State = TrackedEntityState.Unchanged });
            }

            _loaded = true;
        }

        private object ExecuteQuery(Expression expression)
        {
            var predicate = SqlQueryableExpressionHelper.TryBuildPredicate<T>(expression, EntityMapRegistry.GetMap<T>());
            IList<T> rows;
            if (predicate == null)
            {
                EnsureLoaded();
                rows = GetTrackedEntities().Select(x => (T)x.Entity).ToList();
            }
            else
            {
                var map = EntityMapRegistry.GetMap<T>();
                var applyOrgFilter = typeof(ITenantEntity).IsAssignableFrom(typeof(T));
                var tenantOrganizationId = _organizationScope == null || !applyOrgFilter
                    ? null
                    : _organizationScope.GetTenantFilterOrganizationId(typeof(T));
                rows = EntitySqlReader.ReadWhere<T>(
                    _session.Connection,
                    map,
                    predicate,
                    tenantOrganizationId,
                    applyOrgFilter,
                    _session.Transaction);
                TrackReadRows(rows, map);
            }

            return SqlQueryableExpressionHelper.ExecuteInMemory(expression, rows);
        }

        private void TrackReadRows(IList<T> entities, EntityMap map)
        {
            var tracked = _session.GetTrackedEntities(typeof(T));
            var keyProperty = map.EntityType.GetProperty(map.PrimaryKey);
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                var key = keyProperty.GetValue(entity, null);
                var existing = tracked.FirstOrDefault(x => Equals(keyProperty.GetValue(x.Entity, null), key));
                if (existing == null)
                {
                    tracked.Add(new TrackedEntity { Entity = entity, State = TrackedEntityState.Unchanged });
                }
                else
                {
                    entities[index] = (T)existing.Entity;
                }
            }
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
