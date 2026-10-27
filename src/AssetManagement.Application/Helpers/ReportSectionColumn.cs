using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>QA S1.10 (2026-10-27): the Section column (Prep / Senior school) in asset register exports.</summary>
    public static class ReportSectionColumn
    {
        public const string Header = "Section";

        public static IDictionary<int, DepartmentSection> LoadSections(IEnumerable<Department> departments)
        {
            return (departments ?? Enumerable.Empty<Department>())
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Section);
        }

        /// <summary>"Prep" / "Senior school"; blank when the asset has no department (or it is unknown).</summary>
        public static string Label(int? departmentId, IDictionary<int, DepartmentSection> sections)
        {
            DepartmentSection section;
            if (!departmentId.HasValue || sections == null || !sections.TryGetValue(departmentId.Value, out section))
            {
                return string.Empty;
            }

            return DepartmentSectionHelper.Label(section);
        }
    }
}
