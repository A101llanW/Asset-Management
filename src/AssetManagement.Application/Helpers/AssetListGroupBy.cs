using System;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public static class AssetListGroupBy
    {
        public const string Product = "product";

        public const string Department = "department";

        public const string Category = "category";

        public const string Type = "type";

        public const string SubType = "subtype";

        public const string Status = "status";

        public static readonly GroupByOption[] Options = new[]
        {
            new GroupByOption(Product, "By product"),
            new GroupByOption(Department, "By department"),
            new GroupByOption(Category, "By category"),
            new GroupByOption(Type, "By type"),
            new GroupByOption(SubType, "By sub-type"),
            new GroupByOption(Status, "By status")
        };

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Product;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case Department:
                    return Department;
                case Category:
                    return Category;
                case Type:
                    return Type;
                case "assettype":
                    return Type;
                case SubType:
                case "sub-type":
                case "assetsubtype":
                    return SubType;
                case Status:
                    return Status;
                default:
                    return Product;
            }
        }

        public static bool IsProduct(string value)
        {
            return string.Equals(Normalize(value), Product, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDepartment(string value)
        {
            return string.Equals(Normalize(value), Department, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSimpleDimension(string value)
        {
            return !IsProduct(value);
        }

        public static string GetColumnLabel(string value)
        {
            switch (Normalize(value))
            {
                case Department:
                    return "Department";
                case Category:
                    return "Category";
                case Type:
                    return "Type";
                case SubType:
                    return "Sub-type";
                case Status:
                    return "Status";
                default:
                    return "Group";
            }
        }

        /// <summary>
        /// MVC may bind groupBy to AssetFilterVm.GroupBy instead of a separate action parameter.
        /// Prefer the filter value when present.
        /// </summary>
        public static string ResolveFromRequest(AssetFilterVm filter, string groupByParam)
        {
            if (filter != null && !string.IsNullOrWhiteSpace(filter.GroupBy))
            {
                return Normalize(filter.GroupBy);
            }

            return Normalize(groupByParam);
        }
    }

    public sealed class GroupByOption
    {
        public GroupByOption(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; private set; }

        public string Label { get; private set; }
    }
}
