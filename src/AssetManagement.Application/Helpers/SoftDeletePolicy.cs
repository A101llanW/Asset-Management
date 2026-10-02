using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Common;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Soft-delete consistency: tenant master data and long-lived records use
    /// AuditableEntity.IsActive = false (not physical DELETE).
    /// Hard delete is reserved for OrganizationPurgeService and join-table rebuilds
    /// (e.g. RolePermission, DepartmentApprovalStage replace).
    /// </summary>
    public static class SoftDeletePolicy
    {
        public const string AuditMarker = "SoftDeleted";

        public static void MarkInactive(AuditableEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            entity.IsActive = false;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        public static bool IsActiveEntity(AuditableEntity entity)
        {
            return entity != null && entity.IsActive;
        }

        public static void EnsureActive(AuditableEntity entity, string notFoundMessage)
        {
            if (!IsActiveEntity(entity))
            {
                throw new BusinessException(string.IsNullOrWhiteSpace(notFoundMessage)
                    ? "Record not found."
                    : notFoundMessage);
            }
        }

        public static IQueryable<T> WhereActive<T>(this IQueryable<T> query) where T : AuditableEntity
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            return query.Where(x => x.IsActive);
        }

        public static IEnumerable<T> WhereActive<T>(this IEnumerable<T> source) where T : AuditableEntity
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            return source.Where(x => x != null && x.IsActive);
        }

        public static IEnumerable<T> WhereActiveFlag<T>(this IEnumerable<T> source, Func<T, bool> isActive)
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            if (isActive == null)
            {
                throw new ArgumentNullException("isActive");
            }

            return source.Where(isActive);
        }
    }
}
