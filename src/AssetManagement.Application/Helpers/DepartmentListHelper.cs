using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentListHelper
    {
        public static IList<DepartmentVm> DeduplicateById(IEnumerable<DepartmentVm> source)
        {
            return (source ?? Enumerable.Empty<DepartmentVm>())
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();
        }

        public static IDictionary<int, DepartmentVm> ToDictionaryById(IEnumerable<DepartmentVm> source)
        {
            return DeduplicateById(source).ToDictionary(x => x.Id);
        }
    }
}
