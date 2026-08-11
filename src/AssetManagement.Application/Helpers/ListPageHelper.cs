using System;

namespace AssetManagement.Application.Helpers
{
    public static class ListPageHelper
    {
        public static int NormalizePageSize(int pageSize, int defaultSize = 10, int maxSize = 100)
        {
            return pageSize <= 0 ? defaultSize : Math.Min(pageSize, maxSize);
        }

        public static int NormalizePage(int page, int pageSize, int totalCount)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            return Math.Min(Math.Max(page, 1), totalPages);
        }

        public static int ComputeSkip(int page, int pageSize, int totalCount, out int safePage)
        {
            safePage = NormalizePage(page, pageSize, totalCount);
            return (safePage - 1) * pageSize;
        }

        public static string NormalizeDirection(string direction)
        {
            return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }
    }
}
