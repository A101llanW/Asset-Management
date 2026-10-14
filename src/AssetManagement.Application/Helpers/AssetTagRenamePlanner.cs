using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    /// <summary>rel1013b: works out which existing tags change and what they become (pure; no DB).</summary>
    public static class AssetTagRenamePlanner
    {
        public const int MaxBatchSize = 5000;

        /// <summary>The department and every descendant (cycle-safe).</summary>
        public static HashSet<int> GetSubtree(int rootId, IEnumerable<Department> departments)
        {
            var list = (departments ?? Enumerable.Empty<Department>()).ToList();
            var children = list.Where(x => x.ParentDepartmentId.HasValue).ToLookup(x => x.ParentDepartmentId.Value, x => x.Id);
            var result = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(rootId);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!result.Add(id))
                {
                    continue;
                }

                foreach (var child in children[id])
                {
                    stack.Push(child);
                }
            }

            return result;
        }

        /// <summary>True when the tag already has the target shape PREFIX-digits, so it is left alone.</summary>
        public static bool AlreadyConforms(string tag, string prefix)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            return Regex.IsMatch(tag.Trim(), "^" + Regex.Escape(prefix) + "-\\d+$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Builds the ordered plan. Candidates are ordered by department then old tag so numbers are stable;
        /// each prefix starts at peekNext(prefix) and skips any number already taken (asset tags + aliases).
        /// </summary>
        public static List<AssetTagRenamePlanItem> BuildPlan(
            IEnumerable<AssetTagRenameCandidate> candidates,
            IDictionary<int, Department> departments,
            string tenantPrefix,
            Func<string, int> peekNext,
            Func<string, ISet<string>> takenTags)
        {
            var items = new List<AssetTagRenamePlanItem>();
            var nextByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var takenByPrefix = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in (candidates ?? Enumerable.Empty<AssetTagRenameCandidate>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.AssetTag))
                .OrderBy(x => x.DepartmentId ?? 0)
                .ThenBy(x => x.AssetTag, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.AssetId))
            {
                var prefix = AssetTagPrefixResolver.Resolve(candidate.DepartmentId, departments, tenantPrefix).Prefix;
                if (AlreadyConforms(candidate.AssetTag, prefix))
                {
                    continue;
                }

                int next;
                if (!nextByPrefix.TryGetValue(prefix, out next))
                {
                    next = Math.Max(1, peekNext == null ? 1 : peekNext(prefix));
                }

                ISet<string> taken;
                if (!takenByPrefix.TryGetValue(prefix, out taken))
                {
                    taken = new HashSet<string>(takenTags == null ? new HashSet<string>() : (IEnumerable<string>)takenTags(prefix), StringComparer.OrdinalIgnoreCase);
                    takenByPrefix[prefix] = taken;
                }

                var newTag = AssetTagPrefixResolver.FormatTag(prefix, next);
                while (taken.Contains(newTag))
                {
                    next++;
                    newTag = AssetTagPrefixResolver.FormatTag(prefix, next);
                }

                taken.Add(newTag);
                nextByPrefix[prefix] = next + 1;
                items.Add(new AssetTagRenamePlanItem
                {
                    AssetId = candidate.AssetId,
                    AssetName = candidate.AssetName,
                    DepartmentId = candidate.DepartmentId,
                    OldTag = candidate.AssetTag.Trim(),
                    Prefix = prefix,
                    NewTag = newTag
                });
            }

            return items;
        }
    }
}