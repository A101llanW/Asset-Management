using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// QA on 51872c9 (Allan t550s2044, display only): department pickers hide a department that is inactive or that
    /// sits under an inactive ancestor (e.g. class 17C under the hidden Grade 17), unless it is the currently selected
    /// value (so an edit form keeps its value). Data is not changed.
    /// </summary>
    public static class DepartmentPickerVisibility
    {
        /// <summary>Ids of departments that are inactive themselves or have an inactive ancestor in <paramref name="departments"/>.</summary>
        public static ISet<int> HiddenIds(IEnumerable<DepartmentVm> departments)
        {
            var list = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null).ToList();
            var byId = list.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
            var hidden = new HashSet<int>();
            foreach (var department in list)
            {
                if (IsHidden(department, byId))
                {
                    hidden.Add(department.Id);
                }
            }

            return hidden;
        }

        /// <summary>True when the department or any known ancestor is inactive. Cycle-safe; a missing parent ends the walk.</summary>
        public static bool IsHidden(DepartmentVm department, IDictionary<int, DepartmentVm> byId)
        {
            if (department == null)
            {
                return true;
            }

            var seen = new HashSet<int>();
            var current = department;
            while (current != null)
            {
                if (!current.IsActive)
                {
                    return true;
                }

                if (!current.ParentDepartmentId.HasValue || !seen.Add(current.Id))
                {
                    return false;
                }

                DepartmentVm parent;
                if (byId == null || !byId.TryGetValue(current.ParentDepartmentId.Value, out parent))
                {
                    return false;
                }

                current = parent;
            }

            return false;
        }

        /// <summary>The departments a picker may list: visible ones plus the selected value (even when hidden).</summary>
        public static IList<DepartmentVm> VisibleForPicker(IEnumerable<DepartmentVm> departments, int? selectedDepartmentId)
        {
            var list = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null).ToList();
            var hidden = HiddenIds(list);
            return list
                .Where(x => !hidden.Contains(x.Id) || (selectedDepartmentId.HasValue && x.Id == selectedDepartmentId.Value))
                .ToList();
        }
    }
}