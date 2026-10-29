using System;
using System.Collections.Generic;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// QA 188ce56: Departments Details/Edit breadcrumbs show the parent path ("Departments &gt; Academics &gt; Year 6 &gt; 6A")
    /// instead of only "Departments &gt; 6A". Ancestor labels arrive already passed through GradeDisplay
    /// (DepartmentTreeHelper.GetAncestorTrail); each ancestor links to its Details page.
    /// </summary>
    public static class DepartmentBreadcrumbs
    {
        public const string RootLabel = "Departments";

        /// <param name="indexUrl">Departments list URL (first crumb).</param>
        /// <param name="ancestors">Root-first (id, display label) pairs; null or empty = no parent path.</param>
        /// <param name="ancestorUrl">Builds the Details URL for an ancestor id; null = ancestors are not links.</param>
        /// <param name="currentLabel">Display label of this department.</param>
        /// <param name="currentUrl">Link for this department's crumb when a trailing page crumb follows (Edit); else null.</param>
        /// <param name="trailingLabel">Optional last crumb (e.g. "Edit room").</param>
        public static IList<BreadcrumbItemViewModel> Build(
            string indexUrl,
            IList<KeyValuePair<int, string>> ancestors,
            Func<int, string> ancestorUrl,
            string currentLabel,
            string currentUrl,
            string trailingLabel = null)
        {
            var crumbs = new List<BreadcrumbItemViewModel>
            {
                new BreadcrumbItemViewModel { Label = RootLabel, Url = indexUrl }
            };

            if (ancestors != null)
            {
                foreach (var ancestor in ancestors)
                {
                    if (string.IsNullOrWhiteSpace(ancestor.Value))
                    {
                        continue;
                    }

                    crumbs.Add(new BreadcrumbItemViewModel
                    {
                        Label = ancestor.Value,
                        Url = ancestorUrl == null ? null : ancestorUrl(ancestor.Key)
                    });
                }
            }

            var hasTrailing = !string.IsNullOrWhiteSpace(trailingLabel);
            crumbs.Add(new BreadcrumbItemViewModel { Label = currentLabel, Url = hasTrailing ? currentUrl : null });
            if (hasTrailing)
            {
                crumbs.Add(new BreadcrumbItemViewModel { Label = trailingLabel });
            }

            return crumbs;
        }
    }
}
