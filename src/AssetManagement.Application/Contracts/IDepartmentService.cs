using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentService
    {
        IEnumerable<DepartmentVm> GetAll();

        IEnumerable<DepartmentVm> GetRequisitionTargets();

        IEnumerable<DepartmentTreeSectionVm> GetTreeSections();

        IEnumerable<DepartmentTreeSectionVm> GetTreeSections(IEnumerable<DepartmentVm> scopedDepartments);

        DepartmentVm GetById(int id);

        int Create(DepartmentVm model);

        int CreateFromWizard(DepartmentCreateVm model);

        void Update(DepartmentVm model);

        /// <summary>Active children that a Kind/Parent change would make invalid, with valid new-parent candidates.</summary>
        DepartmentKindChangeImpactVm GetKindChangeImpact(int id, AssetManagement.Domain.Enums.DepartmentKind newKind, int? newParentId);

        /// <summary>Inherit choices (Sub-department / Department) that exist in the unit's current ancestry.</summary>
        IList<RequisitionInheritOptionVm> GetRequisitionInheritOptions(int departmentId);
    }
}
