using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Phase 1 stub — full IMemoryCache port follows in batch 2 (reference data reads).
    /// </summary>
    public sealed class ReferenceDataCache : IReferenceDataCache
    {
        public IList<DepartmentVm> GetDepartments(int organizationId, bool activeOnly = true) =>
            throw new NotImplementedException("ReferenceDataCache port pending — use SQL query services.");

        public IList<RoleVm> GetRoles(int organizationId) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IDictionary<string, string> GetSettings(int organizationId) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IList<UserVm> GetUsersForDropdown(int organizationId, int? departmentId = null) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IList<UserVm> GetUsersByIds(int organizationId, IEnumerable<string> userIds) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IList<CategoryLookupVm> GetCategories(int organizationId, bool activeOnly = true) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IList<AssetTypeLookupVm> GetAssetTypes(int organizationId, bool activeOnly = true) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public IList<SupplierVm> GetSuppliers(int organizationId, bool activeOnly = true) =>
            throw new NotImplementedException("ReferenceDataCache port pending.");

        public void InvalidateDepartments(int organizationId)
        {
        }

        public void InvalidateRoles(int organizationId)
        {
        }

        public void InvalidateSettings(int organizationId)
        {
        }
    }
}
