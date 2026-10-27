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

        // rel1018b: tenant defaults for the per-level "use this segment" switches (SystemSetting rows; missing = the default below).
        public const string UseDepartmentSegmentSettingKey = "AssetTag.Segment.Department";
        public const string UseSubDepartmentSegmentSettingKey = "AssetTag.Segment.SubDepartment";
        public const string UseRoomSegmentSettingKey = "AssetTag.Segment.Room";
        public const bool DefaultUseDepartmentSegment = true;
        public const bool DefaultUseSubDepartmentSegment = false; // keeps the rel1018a shape TOPDEPT-ROOM (sub-unit only when there is no room)
        public const bool DefaultUseRoomSegment = true;
        public const int SubDepartmentSegmentLength = 8;
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

        /// <summary>rel1018b: tenant-level inputs (tenant prefix + segment defaults).</summary>
        public sealed class Options
        {
            public string TenantPrefix { get; set; }
            public bool UseDepartmentSegment { get; set; } = DefaultUseDepartmentSegment;
            public bool UseSubDepartmentSegment { get; set; } = DefaultUseSubDepartmentSegment;
            public bool UseRoomSegment { get; set; } = DefaultUseRoomSegment;

            /// <summary>Migration 106: optional prefix per school section. Missing section = no section prefix for it.</summary>
            public IDictionary<DepartmentSection, string> SectionPrefixes { get; set; }
        }

        /// <summary>The three per-level switches after inheritance.</summary>
        public sealed class SegmentFlags
        {
            public bool UseDepartment { get; set; }
            public bool UseSubDepartment { get; set; }
            public bool UseRoom { get; set; }
        }

        public static Options GetOptions(IDictionary<string, string> settings)
        {
            return new Options
            {
                TenantPrefix = GetTenantPrefix(settings),
                UseDepartmentSegment = ReadBool(settings, UseDepartmentSegmentSettingKey, DefaultUseDepartmentSegment),
                UseSubDepartmentSegment = ReadBool(settings, UseSubDepartmentSegmentSettingKey, DefaultUseSubDepartmentSegment),
                UseRoomSegment = ReadBool(settings, UseRoomSegmentSettingKey, DefaultUseRoomSegment),
                SectionPrefixes = GetSectionPrefixes(settings)
            };
        }

        private static bool ReadBool(IDictionary<string, string> settings, string key, bool fallback)
        {
            string value;
            bool parsed;
            return settings != null && settings.TryGetValue(key, out value) && value != null && bool.TryParse(value.Trim(), out parsed) ? parsed : fallback;
        }

        /// <summary>For each switch, the nearest node (the asset's own node first, then each parent) with a value decides; else the tenant default.</summary>
        public static SegmentFlags ResolveFlags(IList<Department> chain, Options options)
        {
            options = options ?? new Options();
            chain = chain ?? new List<Department>();
            return new SegmentFlags
            {
                UseDepartment = chain.Select(x => x.AssetTagUseDepartmentSegment).FirstOrDefault(x => x.HasValue) ?? options.UseDepartmentSegment,
                UseSubDepartment = chain.Select(x => x.AssetTagUseSubDepartmentSegment).FirstOrDefault(x => x.HasValue) ?? options.UseSubDepartmentSegment,
                UseRoom = chain.Select(x => x.AssetTagUseRoomSegment).FirstOrDefault(x => x.HasValue) ?? options.UseRoomSegment
            };
        }

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
            return ResolveWithOptions(departmentId, departments, new Options { TenantPrefix = tenantPrefix });
        }

        /// <summary>Migration 106 overload: tenant prefix + section prefixes, default segment switches.</summary>
        public static Resolution Resolve(
            int? departmentId,
            IDictionary<int, Department> departments,
            string tenantPrefix,
            IDictionary<DepartmentSection, string> sectionPrefixes)
        {
            return ResolveWithOptions(departmentId, departments, new Options { TenantPrefix = tenantPrefix, SectionPrefixes = sectionPrefixes });
        }

        /// <summary>
        /// Precedence (rel1018b + migration 106): 1) nearest literal override = whole prefix (switches and section prefix do not
        /// apply to it); 2) section prefix of the asset's own unit (Prep / Senior school) in place of the department segment,
        /// followed by the sub-department / room segments switched on for this node; 3) tenant-wide prefix; 4) derived prefix
        /// from the department / sub-department / room segments switched on for this node.
        /// </summary>
        public static Resolution ResolveWithOptions(int? departmentId, IDictionary<int, Department> departments, Options options)
        {
            options = options ?? new Options();
            var tenantPrefix = options.TenantPrefix;
            var sectionPrefixes = options.SectionPrefixes;
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
                return DeriveWithSectionPrefix(chain, NormalizePrefix(sectionPrefix), ResolveFlags(chain, options));
            }

            var tenant = NormalizePrefix(tenantPrefix);
            if (tenant != null)
            {
                return new Resolution { Prefix = tenant, Source = SourceTenantDefault };
            }

            return Derive(chain, ResolveFlags(chain, options));
        }

        /// <summary>
        /// SECTIONPREFIX-ROOM (e.g. PP-GALA) with default switches: the section prefix replaces the department segment.
        /// </summary>
        public static Resolution DeriveWithSectionPrefix(IList<Department> chain, string sectionPrefix)
        {
            return DeriveWithSectionPrefix(chain, sectionPrefix, new SegmentFlags { UseDepartment = DefaultUseDepartmentSegment, UseSubDepartment = DefaultUseSubDepartmentSegment, UseRoom = DefaultUseRoomSegment });
        }

        /// <summary>
        /// SECTIONPREFIX [-SUB(8)] [-ROOM(8)]: the section prefix takes the place of the department segment and the
        /// sub-department / room segments follow the per-level switches (same slot rules as <see cref="Derive(IList{Department}, SegmentFlags)"/>).
        /// When no sub/room segment applies (e.g. an asset held by the top department) and the department switch is on,
        /// the department segment (6) follows the prefix so the tag still names the place (PP-PREP). A segment equal to the
        /// previous one is not repeated, and the last segment is shortened so the whole prefix stays within MaxPrefixLength.
        /// </summary>
        public static Resolution DeriveWithSectionPrefix(IList<Department> chain, string sectionPrefix, SegmentFlags flags)
        {
            flags = flags ?? new SegmentFlags { UseDepartment = true, UseRoom = true };
            var top = chain[chain.Count - 1];
            var roomNode = chain.FirstOrDefault(x => IsRoomKind(x.DepartmentKind) && !ReferenceEquals(x, top));
            var subNode = chain.Take(chain.Count - 1).FirstOrDefault(x => !IsRoomKind(x.DepartmentKind));
            var roomSlot = roomNode ?? (chain.Count > 1 ? chain[0] : null);
            if (roomNode == null && flags.UseSubDepartment && ReferenceEquals(roomSlot, subNode))
            {
                roomSlot = null;
            }

            var parts = new List<KeyValuePair<string, Department>>();
            if (flags.UseSubDepartment && subNode != null)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(subNode, SubDepartmentSegmentLength), subNode));
            }

            if (flags.UseRoom && roomSlot != null)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(roomSlot, RoomSegmentLength), roomSlot));
            }

            if (parts.Count == 0 && flags.UseDepartment)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(top, IsRoomKind(top.DepartmentKind) ? RoomSegmentLength : DepartmentSegmentLength), top));
            }

            var prefix = sectionPrefix;
            var lastSegment = sectionPrefix;
            Department sourceNode = parts.Count == 0 ? chain[0] : parts[parts.Count - 1].Value;
            foreach (var part in parts)
            {
                if (string.Equals(part.Key, lastSegment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var room = MaxPrefixLength - prefix.Length - 1;
                if (room <= 0)
                {
                    break;
                }

                var segment = part.Key.Length > room ? part.Key.Substring(0, room) : part.Key;
                prefix = prefix + "-" + segment;
                lastSegment = part.Key;
            }

            return new Resolution
            {
                Prefix = NormalizePrefix(prefix) ?? FallbackPrefix,
                Source = SourceSectionPrefix,
                SourceDepartmentId = sourceNode.Id,
                SourceDepartmentName = sourceNode.Name
            };
        }

        public static Resolution Derive(IList<Department> chain)
        {
            return Derive(chain, new SegmentFlags { UseDepartment = DefaultUseDepartmentSegment, UseSubDepartment = DefaultUseSubDepartmentSegment, UseRoom = DefaultUseRoomSegment });
        }

        /// <summary>
        /// DEPT(6) [-SUB(8)] [-ROOM(8)] from the switched-on levels. SUB = nearest non-room node below the top department.
        /// With the room switch on and no room, the asset's own sub-unit fills the room slot (rel1018a behaviour) unless
        /// the sub-department segment already shows it. Equal adjacent segments are collapsed. All switches off = the
        /// department segment anyway (a prefix cannot be empty).
        /// </summary>
        public static Resolution Derive(IList<Department> chain, SegmentFlags flags)
        {
            if (chain == null || chain.Count == 0)
            {
                return new Resolution { Prefix = FallbackPrefix, Source = SourceFallback };
            }

            flags = flags ?? new SegmentFlags { UseDepartment = true, UseRoom = true };
            var top = chain[chain.Count - 1];
            var roomNode = chain.FirstOrDefault(x => IsRoomKind(x.DepartmentKind) && !ReferenceEquals(x, top));
            var subNode = chain.Take(chain.Count - 1).FirstOrDefault(x => !IsRoomKind(x.DepartmentKind));
            var roomSlot = roomNode ?? (chain.Count > 1 ? chain[0] : null);
            if (roomNode == null && flags.UseSubDepartment && ReferenceEquals(roomSlot, subNode))
            {
                roomSlot = null;
            }

            var parts = new List<KeyValuePair<string, Department>>();
            if (flags.UseDepartment)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(top, DepartmentSegmentLength), top));
            }

            if (flags.UseSubDepartment && subNode != null)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(subNode, SubDepartmentSegmentLength), subNode));
            }

            if (flags.UseRoom && roomSlot != null)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(roomSlot, RoomSegmentLength), roomSlot));
            }

            if (parts.Count == 0)
            {
                parts.Add(new KeyValuePair<string, Department>(Segment(top, DepartmentSegmentLength), top));
            }

            var segments = new List<string>();
            foreach (var part in parts)
            {
                if (segments.Count == 0 || !string.Equals(segments[segments.Count - 1], part.Key, StringComparison.OrdinalIgnoreCase))
                {
                    segments.Add(part.Key);
                }
            }

            var last = parts[parts.Count - 1].Value;
            return new Resolution
            {
                Prefix = NormalizePrefix(string.Join("-", segments)) ?? FallbackPrefix,
                Source = SourceDerivedDefault,
                SourceDepartmentId = last.Id,
                SourceDepartmentName = last.Name
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