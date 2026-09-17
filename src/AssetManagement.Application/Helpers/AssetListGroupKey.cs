using System;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public sealed class AssetListGroupKeyParts
    {
        public string GroupBy { get; set; }

        public int? CategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public int? DepartmentId { get; set; }

        public AssetStatus? Status { get; set; }
    }

    public static class AssetListGroupKey
    {
        public static bool TryParse(string groupKey, out AssetListGroupKeyParts parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return false;
            }

            var separatorIndex = groupKey.IndexOf('|');
            if (separatorIndex <= 0 || separatorIndex >= groupKey.Length - 1)
            {
                return false;
            }

            var prefix = groupKey.Substring(0, separatorIndex);
            var token = groupKey.Substring(separatorIndex + 1);
            parts = new AssetListGroupKeyParts();

            switch (prefix)
            {
                case "cat":
                    parts.GroupBy = AssetListGroupBy.Category;
                    parts.CategoryId = ParseRequiredId(token);
                    return parts.CategoryId.HasValue;
                case "type":
                    parts.GroupBy = AssetListGroupBy.Type;
                    parts.AssetTypeId = ParseRequiredId(token);
                    return parts.AssetTypeId.HasValue;
                case "subtype":
                    parts.GroupBy = AssetListGroupBy.SubType;
                    parts.AssetSubTypeId = ParseNullableId(token);
                    return true;
                case "status":
                    parts.GroupBy = AssetListGroupBy.Status;
                    parts.Status = ParseStatus(token);
                    return parts.Status.HasValue;
                case "dept":
                    parts.GroupBy = AssetListGroupBy.Department;
                    parts.DepartmentId = ParseNullableId(token);
                    return true;
                default:
                    return false;
            }
        }

        private static int? ParseRequiredId(string token)
        {
            int id;
            if (!int.TryParse(token, out id) || id <= 0)
            {
                return null;
            }

            return id;
        }

        private static int? ParseNullableId(string token)
        {
            int id;
            if (!int.TryParse(token, out id) || id <= 0)
            {
                return null;
            }

            return id;
        }

        private static AssetStatus? ParseStatus(string token)
        {
            int statusValue;
            if (!int.TryParse(token, out statusValue))
            {
                return null;
            }

            if (!Enum.IsDefined(typeof(AssetStatus), statusValue))
            {
                return null;
            }

            return (AssetStatus)statusValue;
        }
    }
}
