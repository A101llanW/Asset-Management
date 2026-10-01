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
    }
}
