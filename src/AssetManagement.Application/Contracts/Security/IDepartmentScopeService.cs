using System.Linq;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentScopeService
    {
        bool BypassesDepartmentScope { get; }

        /// <summary>
        /// When true, scoped users with asset transfer permission can view and relocate assets
        /// registered to class departments across the organization.
        /// </summary>
        bool IncludesClassDepartmentAssets { get; }

        int? ScopedDepartmentId { get; }

        IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query);

        IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query);

        void EnsureCanAccessAsset(Asset asset);

        void EnsureCanAccessDepartment(Department department);

        void EnsureCanAccessDepartmentId(int departmentId);

        /// <summary>
        /// Validates that the current user may create a purchase requisition for the given target department
        /// (leaf requisition target), without requiring the user's home department to match when unassigned.
        /// </summary>
        void EnsureCanCreateForRequisitionTarget(Department department);

        int CountVisibleDepartments(bool activeOnly = true);
    }
}
