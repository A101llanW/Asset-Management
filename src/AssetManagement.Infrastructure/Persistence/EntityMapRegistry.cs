using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Persistence
{
    public static class EntityMapRegistry
    {
        private static readonly Dictionary<Type, EntityMap> Maps = new Dictionary<Type, EntityMap>();

        static EntityMapRegistry()
        {
            Register(typeof(Role), "Roles");
            Register(typeof(Permission));
            Register(typeof(RolePermission));
            Register(typeof(Department));
            Register(typeof(Supplier));
            Register(typeof(AssetCategory));
            Register(typeof(AssetType));
            Register(typeof(Asset));
            Register(typeof(AssetRequest));
            Register(typeof(AssetDocument));
            Register(typeof(PurchaseRequest));
            Register(typeof(PurchaseApprovalAction));
            Register(typeof(PurchaseRecord));
            Register(typeof(AssetReceiving));
            Register(typeof(AssetAssignment));
            Register(typeof(AssetTransfer));
            Register(typeof(TransferApprovalAction));
            Register(typeof(AssetReturn));
            Register(typeof(AssetCustodyEvent));
            Register(typeof(AssetMaintenanceRecord));
            Register(typeof(AssetIncident));
            Register(typeof(InsurancePolicy));
            Register(typeof(InsuranceClaim));
            Register(typeof(DepreciationRecord));
            Register(typeof(DisposalRecord));
            Register(typeof(DisposalApprovalAction));
            Register(typeof(Notification));
            Register(typeof(OutboxMessage));
            Register(typeof(AuditLog));
            Register(typeof(WebhookSubscription));
            Register(typeof(WebhookDelivery));
            Register(typeof(Organization));
            Register(typeof(OrganizationLicense));
            Register(typeof(OrganizationLicenseHistory));
            Register(typeof(ImpersonationRequest));
            Register(typeof(SystemSetting));
            Register(typeof(ApplicationUser), "Users", "Id", false);
        }

        public static EntityMap GetMap(Type entityType)
        {
            EntityMap map;
            if (!Maps.TryGetValue(entityType, out map))
            {
                throw new InvalidOperationException("No entity map registered for " + entityType.Name + ".");
            }

            return map;
        }

        public static EntityMap GetMap<T>()
        {
            return GetMap(typeof(T));
        }

        private static void Register(Type entityType, string tableName = null, string primaryKey = "Id", bool primaryKeyIsIdentity = true)
        {
            var resolvedTableName = tableName ?? entityType.Name;
            var scalarProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsScalarProperty)
                .ToList();

            var navigations = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsNavigationProperty)
                .Select(property => CreateNavigationBinding(entityType, property))
                .Where(binding => binding != null)
                .ToList();

            Maps[entityType] = new EntityMap
            {
                EntityType = entityType,
                TableName = resolvedTableName,
                PrimaryKey = primaryKey,
                PrimaryKeyIsIdentity = primaryKeyIsIdentity,
                ScalarProperties = scalarProperties,
                Navigations = navigations
            };
        }

        private static bool IsScalarProperty(PropertyInfo property)
        {
            if (!property.CanWrite || !property.CanRead)
            {
                return false;
            }

            if (IsNavigationProperty(property))
            {
                return false;
            }

            return IsScalarType(property.PropertyType);
        }

        private static bool IsNavigationProperty(PropertyInfo property)
        {
            if (property.Name == "FullName")
            {
                return false;
            }

            var propertyType = property.PropertyType;
            if (propertyType == typeof(string) || propertyType == typeof(byte[]))
            {
                return false;
            }

            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            {
                return true;
            }

            return propertyType.IsClass;
        }

        private static bool IsScalarType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive
                || underlying.IsEnum
                || underlying == typeof(string)
                || underlying == typeof(decimal)
                || underlying == typeof(DateTime)
                || underlying == typeof(byte[]);
        }

        private static NavigationBinding CreateNavigationBinding(Type entityType, PropertyInfo navigationProperty)
        {
            if (typeof(IEnumerable).IsAssignableFrom(navigationProperty.PropertyType))
            {
                return null;
            }

            var foreignKeyName = navigationProperty.Name + "Id";
            var foreignKeyProperty = entityType.GetProperty(foreignKeyName);
            if (foreignKeyProperty == null)
            {
                return null;
            }

            return new NavigationBinding
            {
                NavigationProperty = navigationProperty,
                ForeignKeyProperty = foreignKeyName,
                RelatedEntityType = navigationProperty.PropertyType
            };
        }
    }
}
