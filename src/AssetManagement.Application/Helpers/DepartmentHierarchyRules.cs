using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
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

        public static bool CanCreateRoomUnder(Department parent)
        {
            return CanCreateRoomUnderSubDepartment(parent)
                || IsTopLevelAdministrative(parent);
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
                return CanCreateRoomUnder(parent);
            }

            return false;
        }

        public static bool IsValidRoomParentDepartment(Department department)
        {
            if (department == null)
            {
                return false;
            }

            if (department.DepartmentKind == DepartmentKind.Room
                || department.DepartmentKind == DepartmentKind.Grade
                || department.DepartmentKind == DepartmentKind.Class)
            {
                return false;
            }

            if (department.DepartmentKind == DepartmentKind.SubDepartment)
            {
                return true;
            }

            return department.DepartmentKind == DepartmentKind.Administrative
                && !department.ParentDepartmentId.HasValue;
        }

        public static void AssertValidHierarchy(
            int departmentId,
            DepartmentKind kind,
            int? parentDepartmentId,
            IEnumerable<Department> allDepartments)
        {
            var byId = (allDepartments ?? Enumerable.Empty<Department>()).ToDictionary(x => x.Id);
            if (parentDepartmentId.HasValue && parentDepartmentId.Value == departmentId)
            {
                throw new BusinessException("A department cannot be its own parent.");
            }

            switch (kind)
            {
                case DepartmentKind.Room:
                    AssertValidRoomParent(parentDepartmentId, byId);
                    break;
                case DepartmentKind.SubDepartment:
                    AssertValidSubDepartmentParent(parentDepartmentId, byId);
                    break;
                case DepartmentKind.Administrative:
                    if (parentDepartmentId.HasValue)
                    {
                        throw new BusinessException("Administrative departments must be top-level (no parent department).");
                    }

                    break;
                case DepartmentKind.Grade:
                    if (parentDepartmentId.HasValue)
                    {
                        throw new BusinessException("Grade departments cannot have a parent department.");
                    }

                    break;
                case DepartmentKind.Class:
                    AssertValidClassParent(parentDepartmentId, byId);
                    break;
            }
        }

        private static void AssertValidRoomParent(int? parentDepartmentId, IDictionary<int, Department> byId)
        {
            if (!parentDepartmentId.HasValue)
            {
                return;
            }

            Department parent;
            if (!byId.TryGetValue(parentDepartmentId.Value, out parent))
            {
                throw new BusinessException("Parent department was not found.");
            }

            if (parent.DepartmentKind == DepartmentKind.Room)
            {
                throw new BusinessException("A room cannot be placed under another room.");
            }

            if (parent.DepartmentKind == DepartmentKind.Grade || parent.DepartmentKind == DepartmentKind.Class)
            {
                throw new BusinessException("A room cannot be placed under a grade or class department.");
            }

            if (parent.DepartmentKind == DepartmentKind.SubDepartment)
            {
                if (!parent.ParentDepartmentId.HasValue)
                {
                    throw new BusinessException("Sub-department parent is invalid.");
                }

                Department adminParent;
                if (!byId.TryGetValue(parent.ParentDepartmentId.Value, out adminParent)
                    || !IsTopLevelAdministrativeStructure(adminParent))
                {
                    throw new BusinessException("Sub-department parent must be a top-level administrative department.");
                }

                return;
            }

            if (parent.DepartmentKind == DepartmentKind.Administrative)
            {
                if (parent.ParentDepartmentId.HasValue)
                {
                    throw new BusinessException("A room must be placed under a core administrative department or a sub-department.");
                }

                return;
            }

            throw new BusinessException("The selected parent department type is not valid for a room.");
        }

        private static void AssertValidSubDepartmentParent(int? parentDepartmentId, IDictionary<int, Department> byId)
        {
            if (!parentDepartmentId.HasValue)
            {
                throw new BusinessException("Select a top-level administrative parent department.");
            }

            Department parent;
            if (!byId.TryGetValue(parentDepartmentId.Value, out parent))
            {
                throw new BusinessException("Parent department was not found.");
            }

            if (!IsTopLevelAdministrativeStructure(parent))
            {
                throw new BusinessException("Sub-departments can only be placed under top-level administrative departments.");
            }
        }

        private static void AssertValidClassParent(int? parentDepartmentId, IDictionary<int, Department> byId)
        {
            if (!parentDepartmentId.HasValue)
            {
                throw new BusinessException("Class departments require a grade parent department.");
            }

            Department parent;
            if (!byId.TryGetValue(parentDepartmentId.Value, out parent) || parent.DepartmentKind != DepartmentKind.Grade)
            {
                throw new BusinessException("Class departments must belong to a grade department.");
            }
        }

        private static bool IsTopLevelAdministrativeStructure(Department department)
        {
            return department != null
                && department.DepartmentKind == DepartmentKind.Administrative
                && !department.ParentDepartmentId.HasValue;
        }
    }
}
