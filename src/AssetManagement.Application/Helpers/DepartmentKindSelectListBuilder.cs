using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Builds Kind dropdown items with integer string Values so Edit selection
    /// matches option values (avoids DropDownListFor enum-name vs int mismatch).
    /// </summary>
    public static class DepartmentKindSelectListBuilder
    {
        public sealed class Item
        {
            public string Value { get; set; }
            public string Text { get; set; }
            public bool Selected { get; set; }
        }

        public static string ToOptionValue(DepartmentKind kind)
        {
            return ((int)kind).ToString();
        }

        public static IList<Item> BuildItems(DepartmentKind selectedKind)
        {
            var selectedValue = ToOptionValue(selectedKind);
            return Enum.GetValues(typeof(DepartmentKind))
                .Cast<DepartmentKind>()
                .Select(value =>
                {
                    var optionValue = ToOptionValue(value);
                    return new Item
                    {
                        Value = optionValue,
                        Text = DepartmentOrgHierarchyDisplay.FormatDepartmentKind(value),
                        Selected = string.Equals(optionValue, selectedValue, StringComparison.Ordinal)
                    };
                })
                .ToList();
        }
    }
}
