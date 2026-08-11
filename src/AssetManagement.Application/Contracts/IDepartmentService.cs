using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentService
    {
        IEnumerable<DepartmentVm> GetAll();

        IEnumerable<DepartmentVm> GetRequisitionTargets();

        IEnumerable<DepartmentTreeSectionVm> GetTreeSections();

        DepartmentVm GetById(int id);

        int Create(DepartmentVm model);

        int CreateFromWizard(DepartmentCreateVm model);

        void Update(DepartmentVm model);
    }
}
