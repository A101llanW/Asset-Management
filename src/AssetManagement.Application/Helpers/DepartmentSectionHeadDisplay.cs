using System;
using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>One "Head (Prep)" / "Head (Senior school)" line on department Details.</summary>
    public sealed class DepartmentSectionHeadRow
    {
        public DepartmentSection Section { get; set; }

        public string SectionLabel { get; set; }

        /// <summary>Who approves the "Department Head" stage for this section (null = fallback to role holders).</summary>
        public string HeadName { get; set; }

        /// <summary>"Set on this unit", "From <ancestor>", or the fallback explanation.</summary>
        public string Source { get; set; }

        public bool IsFallback { get; set; }
    }

    /// <summary>Migration 107: effective section heads of a unit, for Details (same walk as the approval resolver).</summary>
    public static class DepartmentSectionHeadDisplay
    {
        public const string FallbackText = "Not set - the \"Department Head\" approval stage uses the Department Head role holders (unchanged rule).";

        public static IList<DepartmentSectionHeadRow> Build(
            DepartmentVm unit,
            Func<int, DepartmentVm> getById,
            Func<string, string> userLabel,
            Func<string, bool> isActiveUser)
        {
            var rows = new List<DepartmentSectionHeadRow>();
            if (unit == null)
            {
                return rows;
            }

            // Departments / sub-departments serve both sections; a year, classroom or room only its own.
            var sections = unit.DepartmentKind == DepartmentKind.Administrative || unit.DepartmentKind == DepartmentKind.SubDepartment
                ? new[] { DepartmentSection.Prep, DepartmentSection.SeniorSchool }
                : new[] { DepartmentSectionHelper.Normalize(unit.Section) };
            foreach (var section in sections)
            {
                rows.Add(BuildRow(unit, section, getById, userLabel, isActiveUser));
            }

            return rows;
        }

        private static DepartmentSectionHeadRow BuildRow(
            DepartmentVm unit,
            DepartmentSection section,
            Func<int, DepartmentVm> getById,
            Func<string, string> userLabel,
            Func<string, bool> isActiveUser)
        {
            var row = new DepartmentSectionHeadRow { Section = section, SectionLabel = DepartmentSectionHelper.Label(section) };
            var visited = new HashSet<int>();
            var current = unit;
            var depth = 0;
            while (current != null && depth < 32 && visited.Add(current.Id))
            {
                var raw = section == DepartmentSection.Prep ? current.PrepHeadUserId : current.SeniorHeadUserId;
                var head = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                if (head != null && (isActiveUser == null || isActiveUser(head)))
                {
                    var label = userLabel == null ? null : userLabel(head);
                    row.HeadName = string.IsNullOrWhiteSpace(label) ? head : label;
                    row.Source = depth == 0 ? "Set on this unit" : "From " + current.Name;
                    return row;
                }

                current = current.ParentDepartmentId.HasValue && getById != null ? getById(current.ParentDepartmentId.Value) : null;
                depth++;
            }

            row.IsFallback = true;
            row.Source = FallbackText;
            return row;
        }
    }
}
