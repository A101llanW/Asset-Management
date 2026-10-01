using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentRoomParentCandidates
    {
        public static int? ResolveAdministrativeAncestorId(DepartmentVm room, IList<DepartmentVm> scopedDepartments)
        {
            if (room == null || !room.ParentDepartmentId.HasValue)
            {
                return null;
            }

            var byId = DepartmentListHelper.ToDictionaryById(scopedDepartments);
            return ResolveAdministrativeAncestorId(room.ParentDepartmentId.Value, byId);
        }

        public static int? ResolveAdministrativeAncestorId(int departmentId, IDictionary<int, DepartmentVm> byId)
        {
            var visited = new HashSet<int>();
            var currentId = departmentId;
            while (currentId > 0 && visited.Add(currentId))
            {
                DepartmentVm current;
                if (!byId.TryGetValue(currentId, out current))
                {
                    return null;
                }

                if (DepartmentOrgHierarchyDisplay.IsTopLevelAdministrative(current))
                {
                    return current.Id;
                }

                if (current.DepartmentKind == DepartmentKind.Room
                    || current.DepartmentKind == DepartmentKind.Grade
                    || current.DepartmentKind == DepartmentKind.Class)
                {
                    return null;
                }

                if (current.DepartmentKind == DepartmentKind.SubDepartment && current.ParentDepartmentId.HasValue)
                {
                    currentId = current.ParentDepartmentId.Value;
                    continue;
                }

                if (current.DepartmentKind == DepartmentKind.Administrative && current.ParentDepartmentId.HasValue)
                {
                    currentId = current.ParentDepartmentId.Value;
                    continue;
                }

                return null;
            }

            return null;
        }

        public static bool IsValidRoomParentDepartment(DepartmentVm department)
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

        public static IList<DepartmentVm> GetRoomParentCandidates(DepartmentVm room, IEnumerable<DepartmentVm> scopedDepartments)
        {
            var list = DepartmentListHelper.DeduplicateById(scopedDepartments);
            if (room == null)
            {
                return new List<DepartmentVm>();
            }

            var byId = DepartmentListHelper.ToDictionaryById(list);
            var adminAncestorId = ResolveAdministrativeAncestorId(room, list);
            var candidates = new List<DepartmentVm>();
            if (adminAncestorId.HasValue)
            {
                candidates = list
                    .Where(x => x.DepartmentKind == DepartmentKind.SubDepartment
                        && x.ParentDepartmentId == adminAncestorId.Value)
                    .OrderBy(x => x.Code)
                    .ToList();
            }

            if (room.ParentDepartmentId.HasValue)
            {
                DepartmentVm currentParent;
                if (!byId.TryGetValue(room.ParentDepartmentId.Value, out currentParent))
                {
                    currentParent = new DepartmentVm
                    {
                        Id = room.ParentDepartmentId.Value,
                        Name = room.ParentDepartmentName ?? ("Department #" + room.ParentDepartmentId.Value),
                        Code = "UNKNOWN",
                        DepartmentKind = DepartmentKind.Administrative,
                        IsActive = false
                    };
                }

                if (IsValidRoomParentDepartment(currentParent)
                    && !candidates.Any(x => x.Id == currentParent.Id))
                {
                    candidates.Insert(0, currentParent);
                }
            }

            return candidates;
        }

        public static IList<DepartmentVm> GetTopLevelAdministrativeParents(IEnumerable<DepartmentVm> scopedDepartments)
        {
            return (scopedDepartments ?? Enumerable.Empty<DepartmentVm>())
                .Where(DepartmentOrgHierarchyDisplay.IsTopLevelAdministrative)
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static IList<RoomOtherParentGroupVm> BuildOtherParentPickerGroups(IEnumerable<DepartmentVm> scopedDepartments)
        {
            var list = DepartmentListHelper.DeduplicateById(scopedDepartments);
            var groups = new List<RoomOtherParentGroupVm>();
            foreach (var admin in GetTopLevelAdministrativeParents(list))
            {
                var group = new RoomOtherParentGroupVm
                {
                    AdminId = admin.Id,
                    AdminLabel = DepartmentLabelHelper.FormatCodeName(admin.Code, admin.Name),
                    SubDepartments = list
                        .Where(x => x.DepartmentKind == DepartmentKind.SubDepartment
                            && x.ParentDepartmentId == admin.Id)
                        .OrderBy(x => x.Code)
                        .Select(x => new RoomOtherParentSubOptionVm
                        {
                            SubDepartmentId = x.Id,
                            Label = DepartmentLabelHelper.FormatCodeName(x.Code, x.Name)
                        })
                        .ToList()
                };
                groups.Add(group);
            }

            return groups;
        }
    }
}
