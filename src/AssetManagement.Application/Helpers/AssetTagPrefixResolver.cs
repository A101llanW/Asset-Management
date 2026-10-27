using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Works out how a new canonical asset tag starts (rel1013a).
    /// Tag = PREFIX + "-" + sequence (at least 3 digits, e.g. SCI-LAB1-001), the same "-NNN" suffix the
    /// older DEPT-TYPE-NNN generator used. Precedence, most specific first, walking up the department tree
    /// from the asset's own department/room: the first node with an AssetTagPrefix override wins (so a room
    /// beats its nearest sub-department ancestor, which beats the top department), then the tenant-wide
    /// prefix setting, then the derived default TOPDEPT-ROOM (or just TOPDEPT when the asset is not in a room
    /// or sub-unit).
    /// Section prefixes (migration 106): an optional prefix per school section (Prep / Senior school). It sits
    /// BELOW the node overrides (an explicit room/department override still wins) and ABOVE the tenant-wide
    /// prefix, and is chosen by the Section of the asset's own department: Prep room Gala + section prefix PP
    /// gives PP-GALA-001, while Senior school keeps its normal prefix. No section prefix set = nothing changes.
    /// </summary>
    public static class AssetTagPrefixResolver
    {
        public const string EnabledSettingKey = "AssetTag.Prefix.Enabled";
        public const string TenantPrefixSettingKey = "AssetTag.Prefix.TenantDefault";
        public const string PrepPrefixSettingKey = "AssetTag.Prefix.Section.Prep";
        public const string SeniorPrefixSettingKey = "AssetTag.Prefix.Section.Senior";
        public const int MaxPrefixLength = 20;
        public const int DepartmentSegmentLength = 6;
        public const int RoomSegmentLength = 8;
        public const string FallbackPrefix = "GEN";
        private const int MaxDepth = 32;

        public const string SourceRoomOverride = "Room override";
        public const string SourceSubDepartmentOverride = "Sub-department override";
        public const string SourceDepartmentOverride = "Department override";
        public const string SourceSectionPrefix = "Section prefix + room";
        public const string SourceTenantDefault = "Tenant default prefix";
        public const string SourceDerivedDefault = "Derived default (department + room)";
        public const string SourceFallback = "Fallback (no department)";

        public sealed class Resolution
        {
            public string Prefix { get; set; }
            public string Source { get; set; }
            public int? SourceDepartmentId { get; set; }
            public string SourceDepartmentName { get; set; }
        }

        /// <summary>Structured tags are on unless the tenant sets AssetTag.Prefix.Enabled=false (then the legacy random 10-char tag is used).</summary>
        public static bool IsEnabled(IDictionary<string, string> settings)
        {
            string value;
            if (settings == null || !settings.TryGetValue(EnabledSettingKey, out value) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            bool parsed;
            return !bool.TryParse(value.Trim(), out parsed) || parsed;
        }

        public static string GetTenantPrefix(IDictionary<string, string> settings)
        {
            string value;
            return settings != null && settings.TryGetValue(TenantPrefixSettingKey, out value) ? NormalizePrefix(value) : null;
        }

        public static string SectionSettingKey(DepartmentSection section)
        {
            return section == DepartmentSection.Prep ? PrepPrefixSettingKey : SeniorPrefixSettingKey;
        }

        /// <summary>The configured section prefixes (normalized). A section without a usable value is absent, so nothing changes for it.</summary>
        public static IDictionary<DepartmentSection, string> GetSectionPrefixes(IDictionary<string, string> settings)
        {
            var result = new Dictionary<DepartmentSection, string>();
            if (settings == null)
            {
                return result;
            }

            foreach (var section in new[] { DepartmentSection.Prep, DepartmentSection.SeniorSchool })
            {
                string value;
                var normalized = settings.TryGetValue(SectionSettingKey(section), out value) ? NormalizePrefix(value) : null;
                if (normalized != null)
                {
                    result[section] = normalized;
                }
            }

            return result;
        }

        /// <summary>Upper-cases, keeps A-Z, 0-9 and single hyphens, trims hyphens, caps the length. Returns null when nothing usable is left.</summary>
        public static string NormalizePrefix(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cleaned = Regex.Replace(raw.Trim().ToUpperInvariant(), @"[\s_]+", "-");
            cleaned = Regex.Replace(cleaned, @"[^A-Z0-9\-]", string.Empty);
            cleaned = Regex.Replace(cleaned, @"-{2,}", "-").Trim('-');
            if (cleaned.Length > MaxPrefixLength)
            {
                cleaned = cleaned.Substring(0, MaxPrefixLength).TrimEnd('-');
            }

            return cleaned.Length == 0 ? null : cleaned;
        }

        public static bool IsRoomKind(DepartmentKind kind)
        {
            return kind == DepartmentKind.Room || kind == DepartmentKind.Class;
        }

        /// <summary>The department itself first, then each parent up to the root. Stops on cycles or missing parents.</summary>
        public static IList<Department> GetChain(int? departmentId, IDictionary<int, Department> departments)
        {
            var chain = new List<Department>();
            if (!departmentId.HasValue || departments == null)
            {
                return chain;
            }

            var seen = new HashSet<int>();
            var currentId = departmentId;
            while (currentId.HasValue && chain.Count < MaxDepth && seen.Add(currentId.Value))
            {
                Department node;
                if (!departments.TryGetValue(currentId.Value, out node) || node == null)
                {
                    break;
                }

                chain.Add(node);
                currentId = node.ParentDepartmentId;
            }

            return chain;
        }

        public static Resolution Resolve(int? departmentId, IDictionary<int, Department> departments, string tenantPrefix)
        {
            return Resolve(departmentId, departments, tenantPrefix, null);
        }

        public static Resolution Resolve(
            int? departmentId,
            IDictionary<int, Department> departments,
            string tenantPrefix,
            IDictionary<DepartmentSection, string> sectionPrefixes)
        {
            var chain = GetChain(departmentId, departments);
            for (var i = 0; i < chain.Count; i++)
            {
                var node = chain[i];
                var overridePrefix = NormalizePrefix(node.AssetTagPrefix);
                if (overridePrefix == null)
                {
                    continue;
                }

                string source;
                if (IsRoomKind(node.DepartmentKind))
                {
                    source = SourceRoomOverride;
                }
                else if (i < chain.Count - 1)
                {
                    source = SourceSubDepartmentOverride;
                }
                else
                {
                    source = SourceDepartmentOverride;
                }

                return new Resolution { Prefix = overridePrefix, Source = source, SourceDepartmentId = node.Id, SourceDepartmentName = node.Name };
            }

            string sectionPrefix;
            if (chain.Count > 0
                && sectionPrefixes != null
                && sectionPrefixes.TryGetValue(chain[0].Section, out sectionPrefix)
                && NormalizePrefix(sectionPrefix) != null)
            {
                return DeriveWithSectionPrefix(chain, NormalizePrefix(sectionPrefix));
            }

            var tenant = NormalizePrefix(tenantPrefix);
            if (tenant != null)
            {
                return new Resolution { Prefix = tenant, Source = SourceTenantDefault };
            }

            return Derive(chain);
        }

        /// <summary>
        /// SECTIONPREFIX-ROOM (e.g. PP-GALA): the section prefix replaces the department part. The room segment comes from the
        /// nearest Room/Class node, else the asset's own node (8 characters; a top department 6). It is not repeated when it equals
        /// the section prefix and is shortened so the whole prefix stays within MaxPrefixLength.
        /// </summary>
        public static Resolution DeriveWithSectionPrefix(IList<Department> chain, string sectionPrefix)
        {
            var node = chain.FirstOrDefault(x => IsRoomKind(x.DepartmentKind)) ?? chain[0];
            var isTop = ReferenceEquals(node, chain[chain.Count - 1]) && !IsRoomKind(node.DepartmentKind);
            var segment = Segment(node, isTop ? DepartmentSegmentLength : RoomSegmentLength);
            var prefix = sectionPrefix;
            if (!string.Equals(segment, sectionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var room = Math.Max(0, MaxPrefixLength - sectionPrefix.Length - 1);
                if (room > 0)
                {
                    prefix = sectionPrefix + "-" + (segment.Length > room ? segment.Substring(0, room) : segment);
                }
            }

            return new Resolution
            {
                Prefix = NormalizePrefix(prefix) ?? FallbackPrefix,
                Source = SourceSectionPrefix,
                SourceDepartmentId = node.Id,
                SourceDepartmentName = node.Name
            };
        }

        public static Resolution Derive(IList<Department> chain)
        {
            if (chain == null || chain.Count == 0)
            {
                return new Resolution { Prefix = FallbackPrefix, Source = SourceFallback };
            }

            var top = chain[chain.Count - 1];
            var deptSegment = Segment(top, DepartmentSegmentLength);
            // The room is the nearest Room/Class node; otherwise the asset's own sub-unit when it is below the top department.
            var room = chain.FirstOrDefault(x => IsRoomKind(x.DepartmentKind)) ?? (chain.Count > 1 ? chain[0] : null);
            var prefix = deptSegment;
            if (room != null && !ReferenceEquals(room, top))
            {
                var roomSegment = Segment(room, RoomSegmentLength);
                if (!string.Equals(roomSegment, deptSegment, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = deptSegment + "-" + roomSegment;
                }
            }

            return new Resolution
            {
                Prefix = NormalizePrefix(prefix) ?? FallbackPrefix,
                Source = SourceDerivedDefault,
                SourceDepartmentId = (room ?? top).Id,
                SourceDepartmentName = (room ?? top).Name
            };
        }

        public static string FormatTag(string prefix, int sequence)
        {
            return prefix + "-" + Math.Max(1, sequence).ToString("000");
        }

        private static string Segment(Department department, int maxLength)
        {
            var code = AssetTagHelper.ResolveDepartmentCode(department);
            return code.Length <= maxLength ? code : code.Substring(0, maxLength);
        }
    }
}