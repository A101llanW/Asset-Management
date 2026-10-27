using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// QA W5.4 (2026-10-27): report preview / PDF rows show "Year" for department names (Department, From, To columns).
    /// Display only: returns copies, so the CSV rows (which share the same arrays) keep the stored names;
    /// code and tag columns are never touched.
    /// </summary>
    public static class ReportYearLabels
    {
        private static readonly HashSet<string> DepartmentHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Department", "Departments", "From", "To", "From Department", "To Department"
        };

        public static IList<IList<string>> Apply(IList<string> headers, IList<IList<string>> rows)
        {
            if (headers == null || rows == null)
            {
                return rows;
            }

            var columns = Enumerable.Range(0, headers.Count)
                .Where(i => headers[i] != null && DepartmentHeaders.Contains(headers[i].Trim()))
                .ToList();
            if (columns.Count == 0)
            {
                return rows;
            }

            var result = new List<IList<string>>(rows.Count);
            foreach (var row in rows)
            {
                if (row == null)
                {
                    result.Add(row);
                    continue;
                }

                var copy = row.ToArray();
                foreach (var column in columns)
                {
                    if (column < copy.Length)
                    {
                        copy[column] = GradeDisplay.ToYear(copy[column]);
                    }
                }

                result.Add(copy);
            }

            return result;
        }
    }
}
