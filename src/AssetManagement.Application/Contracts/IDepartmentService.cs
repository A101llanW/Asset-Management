using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentService
    {
        IEnumerable<DepartmentVm> GetAll();

        IEnumerable<DepartmentVm> GetRequisitionTargets();

        /// <summary>
        /// Full tree (org + classes). Prefer the domain overload for management Index pages.
        /// </summary>
        IEnumerable<DepartmentTreeSectionVm> GetTreeSections();

        /// <summary>
        /// Domain-scoped tree: "org" = Administrative + SubDepartment; "classes" = Grade + Class.
        /// </summary>
        IEnumerable<DepartmentTreeSectionVm> GetTreeSections(string domain);

        DepartmentVm GetById(int id);

        /// <summary>
        /// Org requisition leaves (Kind=Room plus SubDept/Admin IsRequisitionTarget) with parent name and effective flow summary.
        /// </summary>
        IEnumerable<DepartmentVm> GetRoomRequisitionFlows();

        int Create(DepartmentVm model);

        int CreateFromWizard(DepartmentCreateVm model);

        void Update(DepartmentVm model);

        IEnumerable<DepartmentVm> GetOrganizationalParentCandidates(int excludeDepartmentId);
    }
}
