using System.Collections.Generic;

namespace AssetManagement.Web.ViewModels
{
    public class WorkflowFormConfigVm
    {
        public string UsersByDepartmentJson { get; set; }

        public IList<WorkflowDepartmentUserPairVm> DepartmentUserPairs { get; set; } = new List<WorkflowDepartmentUserPairVm>();

        public IList<WorkflowLockedFieldVm> LockedFields { get; set; } = new List<WorkflowLockedFieldVm>();
    }

    public class WorkflowDepartmentUserPairVm
    {
        public string DepartmentFieldId { get; set; }

        public string UserFieldId { get; set; }

        public bool RequireDepartmentForUsers { get; set; }
    }

    public class WorkflowLockedFieldVm
    {
        public string FieldId { get; set; }

        public string DisplayValue { get; set; }
    }
}
