using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IReferenceDataCache
    {
        IList<DepartmentVm> GetDepartments(int organizationId, bool activeOnly = true);

        IList<RoleVm> GetRoles(int organizationId);

        IDictionary<string, string> GetSettings(int organizationId);

        IList<UserVm> GetUsersForDropdown(int organizationId, int? departmentId = null);

        IList<UserVm> GetUsersByIds(int organizationId, IEnumerable<string> userIds);

        IList<CategoryLookupVm> GetCategories(int organizationId, bool activeOnly = true);

        IList<AssetTypeLookupVm> GetAssetTypes(int organizationId, bool activeOnly = true);

        IList<SupplierVm> GetSuppliers(int organizationId, bool activeOnly = true);

        void InvalidateDepartments(int organizationId);

        void InvalidateRoles(int organizationId);

        void InvalidateSettings(int organizationId);

        /// <summary>
        /// Drops every cached user dropdown list (all departments) for the organization.
        /// Call after a user is created, edited, (de)activated, or has their role/department changed.
        /// </summary>
        void InvalidateUsers(int organizationId);

        /// <summary>
        /// Drops the cached category and asset-type lists for the organization.
        /// Call after a category is created, edited, (de)activated, or imported.
        /// </summary>
        void InvalidateCategories(int organizationId);
    }
}
