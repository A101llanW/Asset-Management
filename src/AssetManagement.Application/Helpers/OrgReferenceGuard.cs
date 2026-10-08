using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Result of checking one approval-stage list (roles + positional approver persons) against an organisation.
    /// Kept roles and persons stay positionally aligned: dropping a stage drops its person slot too.
    /// </summary>
    public sealed class StageGuardResult
    {
        public StageGuardResult()
        {
            RoleIds = new List<int>();
            UserIds = new List<string>();
            DroppedRoleIds = new List<int>();
            DroppedUserIds = new List<string>();
        }

        public IList<int> RoleIds { get; private set; }

        public IList<string> UserIds { get; private set; }

        public IList<int> DroppedRoleIds { get; private set; }

        public IList<string> DroppedUserIds { get; private set; }

        public bool HasDrops
        {
            get { return DroppedRoleIds.Count > 0 || DroppedUserIds.Count > 0; }
        }

        public string RoleCsv
        {
            get { return ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(RoleIds.Select(x => (int?)x)); }
        }

        public string UserCsv
        {
            get { return ApprovalWorkflowSettingsHelper.SerializeStageUserIds(UserIds); }
        }

        /// <summary>"roles 45,41; persons u1" (empty when nothing was dropped).</summary>
        public string DescribeDrops()
        {
            var parts = new List<string>();
            if (DroppedRoleIds.Count > 0)
            {
                parts.Add("roles " + string.Join(",", DroppedRoleIds.Select(x => x.ToString()).ToArray()));
            }

            if (DroppedUserIds.Count > 0)
            {
                parts.Add("persons " + string.Join(",", DroppedUserIds.ToArray()));
            }

            return string.Join("; ", parts.ToArray());
        }
    }

    /// <summary>
    /// Same-organisation guard for ids that point at org-scoped rows (roles, users, departments, categories, ...).
    /// Pure functions only: callers pass the organisation's id sets / membership tests, so this is unit-testable
    /// and shared by every write path (Asset Create/Edit/import, approval settings, departments, snapshots, copy).
    /// Background: 2,644 Test-WF assets carried org 11 role ids in CSV stage columns (no FK, no same-org check).
    /// </summary>
    public static class OrgReferenceGuard
    {
        public const string AuditAction = "Security.CrossOrgReference";

        public const string ForeignRoleMessage =
            "the selected approver role belongs to another company or no longer exists. Choose a role from this company's list.";

        public const string ForeignUserMessage =
            "the selected approver person is not a user in this company. Choose a person from this company or 'Any user in role'.";

        /// <summary>
        /// Checks a stored CSV pair (e.g. Asset.TransferApprovalStageRoleIds + TransferApprovalStageUserIds).
        /// A foreign/missing role drops that stage and its person slot; a foreign person on an own-org role
        /// keeps the stage with "any user in role". Unchanged input round-trips to the same CSV values.
        /// </summary>
        public static StageGuardResult GuardStageCsv(
            string roleCsv,
            string userCsv,
            Func<int, bool> roleInOrg,
            Func<string, bool> userInOrg)
        {
            var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(roleCsv);
            var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(userCsv);
            var result = new StageGuardResult();
            for (var i = 0; i < roles.Count; i++)
            {
                var roleId = roles[i];
                var userId = i < users.Count ? Normalize(users[i]) : null;
                if (roleInOrg != null && !roleInOrg(roleId))
                {
                    result.DroppedRoleIds.Add(roleId);
                    if (userId != null)
                    {
                        result.DroppedUserIds.Add(userId);
                    }

                    continue;
                }

                if (userId != null && userInOrg != null && !userInOrg(userId))
                {
                    result.DroppedUserIds.Add(userId);
                    userId = null;
                }

                result.RoleIds.Add(roleId);
                result.UserIds.Add(userId);
            }

            // Person slots beyond the last role have no stage; they are orphans, not cross-org data. Keep them out.
            return result;
        }

        /// <summary>
        /// Lists posted stages whose role or person is not in the organisation (for interactive rejection).
        /// Returns one message per bad stage, e.g. "Transfer Stage 2: the selected approver role ...".
        /// </summary>
        public static IList<string> FindForeignStages(
            IEnumerable<ApprovalProcessSettingsVm> processes,
            Func<int, bool> roleInOrg,
            Func<string, bool> userInOrg)
        {
            var messages = new List<string>();
            foreach (var process in processes ?? Enumerable.Empty<ApprovalProcessSettingsVm>())
            {
                if (process == null || process.Stages == null)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(process.DisplayName) ? (process.ProcessCode ?? "Approval") : process.DisplayName;
                for (var i = 0; i < process.Stages.Count; i++)
                {
                    var stage = process.Stages[i];
                    if (stage == null)
                    {
                        continue;
                    }

                    if (stage.RoleId.HasValue && stage.RoleId.Value > 0 && roleInOrg != null && !roleInOrg(stage.RoleId.Value))
                    {
                        messages.Add(label + " Stage " + (i + 1) + ": " + ForeignRoleMessage + " (role #" + stage.RoleId.Value + ")");
                        continue;
                    }

                    var userId = Normalize(stage.UserId);
                    if (userId != null && userInOrg != null && !userInOrg(userId))
                    {
                        messages.Add(label + " Stage " + (i + 1) + ": " + ForeignUserMessage);
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// Drop mode for posted/default stages (bulk/import): removes stages with a foreign role (and their person),
        /// clears foreign persons. Mutates <paramref name="processes"/>; returns what was dropped per process code.
        /// </summary>
        public static IDictionary<string, StageGuardResult> DropForeignStages(
            IEnumerable<ApprovalProcessSettingsVm> processes,
            Func<int, bool> roleInOrg,
            Func<string, bool> userInOrg)
        {
            var dropped = new Dictionary<string, StageGuardResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in processes ?? Enumerable.Empty<ApprovalProcessSettingsVm>())
            {
                if (process == null || process.Stages == null)
                {
                    continue;
                }

                var result = new StageGuardResult();
                var kept = new List<ApprovalStageSettingsVm>();
                foreach (var stage in process.Stages)
                {
                    if (stage == null)
                    {
                        kept.Add(stage);
                        continue;
                    }

                    if (stage.RoleId.HasValue && stage.RoleId.Value > 0 && roleInOrg != null && !roleInOrg(stage.RoleId.Value))
                    {
                        result.DroppedRoleIds.Add(stage.RoleId.Value);
                        var orphan = Normalize(stage.UserId);
                        if (orphan != null)
                        {
                            result.DroppedUserIds.Add(orphan);
                        }

                        continue;
                    }

                    var userId = Normalize(stage.UserId);
                    if (userId != null && userInOrg != null && !userInOrg(userId))
                    {
                        result.DroppedUserIds.Add(userId);
                        stage.UserId = null;
                    }

                    kept.Add(stage);
                }

                if (result.HasDrops)
                {
                    process.Stages = kept;
                    dropped[process.ProcessCode ?? string.Empty] = result;
                }
            }

            return dropped;
        }

        /// <summary>Org-copy mapping: maps each role id through <paramref name="roleMap"/> (source id -> target id).
        /// Unmapped roles are dropped with their stage; persons are never copied across organisations.</summary>
        public static StageGuardResult MapStageCsv(string roleCsv, string userCsv, IDictionary<int, int> roleMap)
        {
            var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(roleCsv);
            var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(userCsv);
            var result = new StageGuardResult();
            for (var i = 0; i < roles.Count; i++)
            {
                int mapped;
                if (roleMap != null && roleMap.TryGetValue(roles[i], out mapped) && mapped > 0)
                {
                    result.RoleIds.Add(mapped);
                    result.UserIds.Add(null);
                }
                else
                {
                    result.DroppedRoleIds.Add(roles[i]);
                }
            }

            foreach (var user in users)
            {
                var userId = Normalize(user);
                if (userId != null)
                {
                    result.DroppedUserIds.Add(userId);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds source-role-id -> target-role-id by role name (case/space-insensitive). When a name occurs more
        /// than once in the target, the lowest id wins (deterministic). Source roles with no same-named target
        /// role are left out (callers drop and report them).
        /// </summary>
        public static IDictionary<int, int> BuildRoleMapByName(
            IEnumerable<KeyValuePair<int, string>> sourceRoles,
            IEnumerable<KeyValuePair<int, string>> targetRoles)
        {
            var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in (targetRoles ?? Enumerable.Empty<KeyValuePair<int, string>>()).OrderBy(x => x.Key))
            {
                var key = NormalizeName(target.Value);
                if (target.Key > 0 && key != null && !byName.ContainsKey(key))
                {
                    byName[key] = target.Key;
                }
            }

            var map = new Dictionary<int, int>();
            foreach (var source in sourceRoles ?? Enumerable.Empty<KeyValuePair<int, string>>())
            {
                var key = NormalizeName(source.Value);
                int targetId;
                if (source.Key > 0 && key != null && byName.TryGetValue(key, out targetId) && !map.ContainsKey(source.Key))
                {
                    map[source.Key] = targetId;
                }
            }

            return map;
        }

        /// <summary>True when the optional id is empty or in the allowed set.</summary>
        public static bool IsEmptyOrAllowed(int? id, Func<int, bool> inOrg)
        {
            return !id.HasValue || id.Value <= 0 || inOrg == null || inOrg(id.Value);
        }

        /// <summary>Audit "old values" text: org, column and the offending ids. Never contains secrets.</summary>
        public static string BuildAuditDetail(int organizationId, string column, string ids)
        {
            return "org=" + organizationId + ";column=" + (column ?? string.Empty) + ";ids=" + (ids ?? string.Empty);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return string.Join(" ", value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
