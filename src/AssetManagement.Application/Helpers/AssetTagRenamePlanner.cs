using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

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
            return BuildPlan(candidates, departments, tenantPrefix, peekNext, takenTags, null);
        }

        /// <summary>
        /// Same as BuildPlan, but when levels is set the prefix is built from three levels
        /// (department, sub-department, room) instead of the single most-specific override.
        /// </summary>
        public static List<AssetTagRenamePlanItem> BuildPlan(
            IEnumerable<AssetTagRenameCandidate> candidates,
            IDictionary<int, Department> departments,
            string tenantPrefix,
            Func<string, int> peekNext,
            Func<string, ISet<string>> takenTags,
            LevelOptions levels)
        {
            return BuildPlan(candidates, departments, tenantPrefix, peekNext, takenTags, levels, null);
        }

        /// <summary>Same as above; sectionPrefixes (Prep / Senior school) only applies on the single-prefix path (levels == null), exactly as for new tags.</summary>
        public static List<AssetTagRenamePlanItem> BuildPlan(
            IEnumerable<AssetTagRenameCandidate> candidates,
            IDictionary<int, Department> departments,
            string tenantPrefix,
            Func<string, int> peekNext,
            Func<string, ISet<string>> takenTags,
            LevelOptions levels,
            IDictionary<DepartmentSection, string> sectionPrefixes)
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
                var prefix = levels == null
                    ? AssetTagPrefixResolver.Resolve(candidate.DepartmentId, departments, tenantPrefix, sectionPrefixes).Prefix
                    : BuildLevelPrefix(candidate.DepartmentId, departments, tenantPrefix, levels);
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

        public const string LevelDepartment = "Department";
        public const string LevelSubDepartment = "Sub-department";
        public const string LevelRoom = "Department room";

        /// <summary>Three-level prefix options from the rename page. A box replaces the code of every node at that level; excluded nodes are left out of the tag.</summary>
        public sealed class LevelOptions
        {
            public string DepartmentOverride { get; set; }
            public string SubDepartmentOverride { get; set; }
            public string RoomOverride { get; set; }
            public ISet<int> ExcludedDepartmentIds { get; set; } = new HashSet<int>();
        }

        /// <summary>One code shown in a preview column.</summary>
        public sealed class LevelCode
        {
            public string Level { get; set; }
            public int DepartmentId { get; set; }
            public string DepartmentName { get; set; }
            public string Code { get; set; }
            public string Source { get; set; }
            public bool Included { get; set; }
            public int AssetCount { get; set; }
        }

        /// <summary>Department (top), sub-department (nearest non-room node below the top) and room (nearest Room/Class node below the top). Any can be null.</summary>
        public static void SplitLevels(int? departmentId, IDictionary<int, Department> departments, out Department top, out Department sub, out Department room)
        {
            var chain = AssetTagPrefixResolver.GetChain(departmentId, departments);
            top = chain.Count == 0 ? null : chain[chain.Count - 1];
            var topNode = top;
            room = chain.FirstOrDefault(x => !ReferenceEquals(x, topNode) && AssetTagPrefixResolver.IsRoomKind(x.DepartmentKind));
            sub = chain.FirstOrDefault(x => !ReferenceEquals(x, topNode) && !AssetTagPrefixResolver.IsRoomKind(x.DepartmentKind));
        }

        /// <summary>Code of one level: page box, else the node's own AssetTagPrefix, else its department code.</summary>
        public static LevelCode GetLevelCode(Department node, string level, LevelOptions levels)
        {
            var box = level == LevelDepartment ? levels.DepartmentOverride : level == LevelSubDepartment ? levels.SubDepartmentOverride : levels.RoomOverride;
            var boxCode = AssetTagPrefixResolver.NormalizePrefix(box);
            var own = AssetTagPrefixResolver.NormalizePrefix(node.AssetTagPrefix);
            string code, source;
            if (boxCode != null) { code = boxCode; source = "Prefix box"; }
            else if (own != null) { code = own; source = "Saved prefix override"; }
            else
            {
                code = CompactCode(node);
                source = "Short code from name";
            }

            return new LevelCode
            {
                Level = level,
                DepartmentId = node.Id,
                DepartmentName = node.Name,
                Code = code,
                Source = source,
                Included = levels.ExcludedDepartmentIds == null || !levels.ExcludedDepartmentIds.Contains(node.Id)
            };
        }

        public const int CompactCodeMaxLength = 4;
        private static readonly HashSet<string> CompactNoiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ROOM", "ROOMS", "OFFICE", "OFFICES", "DESK", "DEPARTMENT", "DEPT", "SECTION", "UNIT", "AREA", "THE", "AND", "OF", "&"
        };

        /// <summary>
        /// Default (not typed, not saved) code for one level, kept short so DEPT-SUB-ROOM-NNN stays under the 20-character prefix cap:
        /// drop noise words (Room, Office, Desk, Department, ...); one word left = its first 3 letters (Art room = ART, Music Room = MUS,
        /// Academics = ACA); several words = their initials, up to 4 (Science Lab 1 = SL1). Falls back to the department code, then GEN.
        /// </summary>
        public static string CompactCode(Department node)
        {
            var words = System.Text.RegularExpressions.Regex.Split((node == null ? null : node.Name) ?? string.Empty, "[^A-Za-z0-9]+")
                .Where(x => x.Length > 0)
                .ToList();
            var kept = words.Where(x => !CompactNoiseWords.Contains(x)).ToList();
            if (kept.Count == 0) { kept = words; }
            string code;
            if (kept.Count == 0)
            {
                code = AssetTagHelper.ResolveDepartmentCode(node);
            }
            else if (kept.Count == 1)
            {
                code = kept[0].Length <= 3 ? kept[0] : kept[0].Substring(0, 3);
            }
            else
            {
                code = string.Concat(kept.Select(x => char.IsDigit(x[0]) ? x : x.Substring(0, 1)));
            }

            code = AssetTagPrefixResolver.NormalizePrefix(code) ?? AssetTagPrefixResolver.FallbackPrefix;
            return code.Length > CompactCodeMaxLength ? code.Substring(0, CompactCodeMaxLength) : code;
        }

        /// <summary>DEPT-SUB-ROOM from the included codes. Falls back to the tenant prefix, then GEN, when nothing is included. The sequence number is always added after.</summary>
        public static string BuildLevelPrefix(int? departmentId, IDictionary<int, Department> departments, string tenantPrefix, LevelOptions levels)
        {
            Department top, sub, room;
            SplitLevels(departmentId, departments, out top, out sub, out room);
            var parts = new List<string>();
            foreach (var pair in new[] { Tuple.Create(top, LevelDepartment), Tuple.Create(sub, LevelSubDepartment), Tuple.Create(room, LevelRoom) })
            {
                if (pair.Item1 == null) { continue; }
                var code = GetLevelCode(pair.Item1, pair.Item2, levels);
                if (code.Included && !parts.Contains(code.Code, StringComparer.OrdinalIgnoreCase)) { parts.Add(code.Code); }
            }

            return AssetTagPrefixResolver.NormalizePrefix(string.Join("-", parts))
                ?? AssetTagPrefixResolver.NormalizePrefix(tenantPrefix)
                ?? AssetTagPrefixResolver.FallbackPrefix;
        }

        /// <summary>The distinct codes per level for the candidates (the three preview columns).</summary>
        public static List<LevelCode> GetLevelCodes(IEnumerable<AssetTagRenameCandidate> candidates, IDictionary<int, Department> departments, LevelOptions levels)
        {
            var result = new Dictionary<string, LevelCode>();
            foreach (var candidate in candidates ?? Enumerable.Empty<AssetTagRenameCandidate>())
            {
                Department top, sub, room;
                SplitLevels(candidate.DepartmentId, departments, out top, out sub, out room);
                foreach (var pair in new[] { Tuple.Create(top, LevelDepartment), Tuple.Create(sub, LevelSubDepartment), Tuple.Create(room, LevelRoom) })
                {
                    if (pair.Item1 == null) { continue; }
                    var key = pair.Item2 + ":" + pair.Item1.Id;
                    LevelCode code;
                    if (!result.TryGetValue(key, out code))
                    {
                        code = GetLevelCode(pair.Item1, pair.Item2, levels);
                        result[key] = code;
                    }

                    code.AssetCount++;
                }
            }

            return result.Values.OrderBy(x => x.DepartmentName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}