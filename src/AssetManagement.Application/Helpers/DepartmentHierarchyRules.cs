using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentHierarchyRules
    {
        public static bool IsTopLevelAdministrative(Department department)
        {
            return department != null
                && department.IsActive
                && department.DepartmentKind == DepartmentKind.Administrative
                && !department.ParentDepartmentId.HasValue;
        }

        public static bool CanCreateSubDepartmentUnder(Department parent)
        {
            return IsTopLevelAdministrative(parent);
        }

        public static bool CanCreateRoomUnderSubDepartment(Department parent)
        {
            return parent != null
                && parent.IsActive
                && parent.DepartmentKind == DepartmentKind.SubDepartment;
        }

        public static bool AllowsIndependentRoomCreate()
        {
            return true;
        }

        public static bool IsValidLockedParentForSetupMode(string setupMode, Department parent)
        {
            if (parent == null)
            {
                return false;
            }

            if (setupMode == DepartmentSetupModes.SubDepartment)
            {
                return CanCreateSubDepartmentUnder(parent);
            }

            if (setupMode == DepartmentSetupModes.Room)
            {
                return CanCreateRoomUnderSubDepartment(parent);
            }

            return false;
        }
    }
}
