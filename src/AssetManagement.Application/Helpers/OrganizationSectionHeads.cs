using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Migration 112: company-wide Head of KG / Prep / Senior school (one user per section per tenant).
    /// Department PrepHeadUserId / SeniorHeadUserId / KgHeadUserId (107/111) remain unit-level overrides and win first.
    /// </summary>
    public static class OrganizationSectionHeads
    {
        public const string OrgFallbackSource = "Company section head (Settings)";

        /// <summary>Map Section -> HeadUserId (trimmed; blank entries omitted).</summary>
        public static IDictionary<DepartmentSection, string> ToMap(IEnumerable<OrganizationSectionHead> rows)
        {
            var map = new Dictionary<DepartmentSection, string>();
            if (rows == null)
            {
                return map;
            }

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                var section = DepartmentSectionHelper.Normalize(row.Section);
                var head = string.IsNullOrWhiteSpace(row.HeadUserId) ? null : row.HeadUserId.Trim();
                map[section] = head;
            }

            return map;
        }

        public static string Get(IDictionary<DepartmentSection, string> map, DepartmentSection section)
        {
            if (map == null)
            {
                return null;
            }

            string head;
            return map.TryGetValue(DepartmentSectionHelper.Normalize(section), out head) && !string.IsNullOrWhiteSpace(head)
                ? head.Trim()
                : null;
        }

        /// <summary>
        /// Ensure the three section rows exist for the organisation (HeadUserId left as-is when present).
        /// Returns the tracked rows (existing + newly added).
        /// </summary>
        public static IList<OrganizationSectionHead> EnsureRows(
            IEnumerable<OrganizationSectionHead> existing,
            int organizationId,
            Action<OrganizationSectionHead> add)
        {
            var list = (existing ?? Enumerable.Empty<OrganizationSectionHead>())
                .Where(x => x != null && x.OrganizationId == organizationId)
                .ToList();
            var bySection = list.GroupBy(x => DepartmentSectionHelper.Normalize(x.Section))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var section in DepartmentSectionHelper.All)
            {
                if (bySection.ContainsKey(section))
                {
                    continue;
                }

                var row = new OrganizationSectionHead
                {
                    OrganizationId = organizationId,
                    Section = section,
                    HeadUserId = null
                };
                if (add != null)
                {
                    add(row);
                }

                list.Add(row);
                bySection[section] = row;
            }

            return list;
        }

        /// <summary>Apply posted head user ids (null/blank clears). Unknown users should be rejected by the caller.</summary>
        public static void Apply(
            IEnumerable<OrganizationSectionHead> rows,
            string kgHeadUserId,
            string prepHeadUserId,
            string seniorHeadUserId)
        {
            if (rows == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                switch (DepartmentSectionHelper.Normalize(row.Section))
                {
                    case DepartmentSection.KG:
                        row.HeadUserId = NormalizeUserId(kgHeadUserId);
                        break;
                    case DepartmentSection.Prep:
                        row.HeadUserId = NormalizeUserId(prepHeadUserId);
                        break;
                    default:
                        row.HeadUserId = NormalizeUserId(seniorHeadUserId);
                        break;
                }
            }
        }

        private static string NormalizeUserId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
    }
}
