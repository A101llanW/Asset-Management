using System;
using System.Collections.Generic;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>The section head found for a unit: who, and on which department it is set.</summary>
    public sealed class SectionHeadMatch
    {
        public SectionHeadMatch(string userId, int departmentId, int depth)
        {
            UserId = userId;
            DepartmentId = departmentId;
            Depth = depth;
        }

        public string UserId { get; private set; }

        /// <summary>The department (the unit itself or an ancestor) that carries the head.</summary>
        public int DepartmentId { get; private set; }

        /// <summary>0 = set on the unit itself; 1 = its parent; ...</summary>
        public int Depth { get; private set; }
    }

    /// <summary>
    /// Migration 107: a department can have a different head per school section (Prep / Senior school), regardless of
    /// the person's role. Rules:
    ///   * Which section: the Section of the unit the request is for (a room, classroom, year, sub-department or department).
    ///   * Which head: walk up from that unit (unit, parent, grandparent ...); the first one with a head set for that
    ///     section (and the person still active) wins.
    ///   * None set: callers fall back to the old behaviour (the Department Head role holders of the unit / ancestors).
    /// Classrooms and rooms never carry heads themselves; they take their department's.
    /// </summary>
    public static class DepartmentSectionHeads
    {
        private const int MaxDepth = 32;

        /// <summary>Kinds whose Edit screen offers the two section heads (not classrooms or rooms).</summary>
        public static bool CanHaveSectionHeads(DepartmentKind kind)
        {
            return kind != DepartmentKind.Class && kind != DepartmentKind.Room;
        }

        /// <summary>The head stored on this department for the section (null = not set).</summary>
        public static string Get(Department department, DepartmentSection section)
        {
            if (department == null)
            {
                return null;
            }

            var value = DepartmentSectionHelper.Normalize(section) == DepartmentSection.Prep
                ? department.PrepHeadUserId
                : department.SeniorHeadUserId;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Section head for a request on <paramref name="departmentId"/>, in that unit's section. Null when no unit in the
        /// chain has an (active) head for the section.
        /// </summary>
        public static SectionHeadMatch Resolve(int? departmentId, Func<int, Department> getById, Func<string, bool> isActiveUser)
        {
            if (!departmentId.HasValue || departmentId.Value <= 0 || getById == null)
            {
                return null;
            }

            var start = getById(departmentId.Value);
            if (start == null)
            {
                return null;
            }

            return Resolve(departmentId, DepartmentSectionHelper.Normalize(start.Section), getById, isActiveUser);
        }

        /// <summary>As <see cref="Resolve(int?, Func{int, Department}, Func{string, bool})"/> for an explicit section.</summary>
        public static SectionHeadMatch Resolve(int? departmentId, DepartmentSection section, Func<int, Department> getById, Func<string, bool> isActiveUser)
        {
            if (!departmentId.HasValue || departmentId.Value <= 0 || getById == null)
            {
                return null;
            }

            var visited = new HashSet<int>();
            int? current = departmentId.Value;
            var depth = 0;
            while (current.HasValue && current.Value > 0 && depth < MaxDepth && visited.Add(current.Value))
            {
                var department = getById(current.Value);
                if (department == null)
                {
                    return null;
                }

                var head = Get(department, section);
                if (head != null && (isActiveUser == null || isActiveUser(head)))
                {
                    return new SectionHeadMatch(head, department.Id, depth);
                }

                current = department.ParentDepartmentId;
                depth++;
            }

            return null;
        }
    }
}
