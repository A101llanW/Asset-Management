using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentOrgHierarchyDisplay
    {
        public const int DefaultVisibleChildCount = 10;

        public static string FormatDepartmentKind(DepartmentKind kind)
        {
            switch (kind)
            {
                case DepartmentKind.SubDepartment:
                    return "Sub-department";
                case DepartmentKind.Room:
                    return "Room";
                case DepartmentKind.Administrative:
                    return "Administrative";
                case DepartmentKind.Grade:
                    return "Grade";
                case DepartmentKind.Class:
                    return "Class";
                default:
                    return kind.ToString();
            }
        }

        public static bool IsTopLevelAdministrative(DepartmentVm department)
        {
            return department != null
                && department.DepartmentKind == DepartmentKind.Administrative
                && !department.ParentDepartmentId.HasValue;
        }

        public static IList<DepartmentVm> BuildAdministrativeSectionItems(IEnumerable<DepartmentVm> scopedDepartments)
        {
            var departments = (scopedDepartments ?? Enumerable.Empty<DepartmentVm>()).ToList();
            var admins = departments
                .Where(IsTopLevelAdministrative)
                .OrderBy(x => x.Name)
                .ToList();

            foreach (var admin in admins)
            {
                AttachAdministrativeChildren(admin, departments);
            }

            return admins;
        }

        public static void AttachAdministrativeChildren(DepartmentVm admin, IList<DepartmentVm> scopedDepartments)
        {
            if (admin == null)
            {
                return;
            }

            admin.Children = new List<DepartmentVm>();
            var subs = GetSubDepartmentsUnder(scopedDepartments, admin.Id);
            foreach (var sub in subs)
            {
                sub.Children = GetRoomsUnderSubDepartment(scopedDepartments, sub.Id);
                admin.Children.Add(sub);
            }

            foreach (var room in GetDirectRoomsUnderAdministrative(scopedDepartments, admin.Id))
            {
                admin.Children.Add(room);
            }
        }

        public static IList<DepartmentVm> GetSubDepartmentsUnder(IEnumerable<DepartmentVm> scopedDepartments, int administrativeDepartmentId)
        {
            return (scopedDepartments ?? Enumerable.Empty<DepartmentVm>())
                .Where(x => x.ParentDepartmentId == administrativeDepartmentId
                    && x.DepartmentKind == DepartmentKind.SubDepartment)
                .OrderBy(x => x.Code)
                .ToList();
        }

        public static IList<DepartmentVm> GetDirectRoomsUnderAdministrative(IEnumerable<DepartmentVm> scopedDepartments, int administrativeDepartmentId)
        {
            return (scopedDepartments ?? Enumerable.Empty<DepartmentVm>())
                .Where(x => x.ParentDepartmentId == administrativeDepartmentId
                    && x.DepartmentKind == DepartmentKind.Room)
                .OrderBy(x => x.Code)
                .ToList();
        }

        public static IList<DepartmentVm> GetRoomsUnderSubDepartment(IEnumerable<DepartmentVm> scopedDepartments, int subDepartmentId)
        {
            return (scopedDepartments ?? Enumerable.Empty<DepartmentVm>())
                .Where(x => x.ParentDepartmentId == subDepartmentId
                    && x.DepartmentKind == DepartmentKind.Room)
                .OrderBy(x => x.Code)
                .ToList();
        }

        public static bool IsNodeOrDescendantInSet(DepartmentVm node, ISet<int> visibleIds)
        {
            if (node == null || visibleIds == null || visibleIds.Count == 0)
            {
                return false;
            }

            if (visibleIds.Contains(node.Id))
            {
                return true;
            }

            if (node.Children == null)
            {
                return false;
            }

            foreach (var child in node.Children)
            {
                if (IsNodeOrDescendantInSet(child, visibleIds))
                {
                    return true;
                }
            }

            return false;
        }

        public static void AttachGradeClassChildren(IEnumerable<DepartmentVm> scopedDepartments)
        {
            var departments = (scopedDepartments ?? Enumerable.Empty<DepartmentVm>()).ToList();
            foreach (var grade in departments.Where(x => x.DepartmentKind == DepartmentKind.Grade))
            {
                grade.Children = departments
                    .Where(x => x.ParentDepartmentId == grade.Id && x.DepartmentKind == DepartmentKind.Class)
                    .OrderBy(x => x.Code)
                    .ToList();
            }
        }
    }
}
