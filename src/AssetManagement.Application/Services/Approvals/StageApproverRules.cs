using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// Org-scoped approver-person record used to validate an optional "Approver (person)" on a requisition
    /// approval stage or the "Purchase order raised by" person. Built from live, tenant-filtered user data.
    /// </summary>
    public sealed class ApproverUserRecord
    {
        public string Id { get; set; }

        public int? OrganizationId { get; set; }

        public bool IsActive { get; set; }

        public int? RoleId { get; set; }

        public string Label { get; set; }
    }

    /// <summary>
    /// Rules for the optional per-stage approver person and the PO-raiser person.
    /// A person is only valid when the user is active, belongs to the organisation and holds the chosen role.
    /// A person whose role no longer matches after the role was changed is cleared (not an error).
    /// </summary>
    public static class StageApproverRules
    {
        public const string AnyUserInRoleLabel = "Any user in role";

        /// <summary>
        /// Validates and normalizes posted stages in place. <paramref name="storedRoleIds"/> are the roles stored
        /// before this save (positional), used to detect a role change: a mismatched person on a changed role is
        /// cleared silently; on an unchanged role it is a validation error. Posted ids are never trusted across orgs.
        /// </summary>
        public static void ValidateStages(
            IList<ApprovalStageSettingsVm> stages,
            IList<int> storedRoleIds,
            int organizationId,
            ICollection<int> organizationRoleIds,
            Func<string, ApproverUserRecord> findUser,
            Action<string, string> addError,
            string errorKeyPrefix)
        {
            if (stages == null || addError == null)
            {
                return;
            }

            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                var key = (errorKeyPrefix ?? string.Empty) + "[" + i + "].UserId";
                var label = "Stage " + (i + 1);
                if (stage.RoleId.HasValue && stage.RoleId.Value > 0
                    && organizationRoleIds != null
                    && !organizationRoleIds.Contains(stage.RoleId.Value))
                {
                    addError((errorKeyPrefix ?? string.Empty) + "[" + i + "].RoleId", label + ": the selected role does not exist in this organisation.");
                    stage.UserId = null;
                    continue;
                }

                int? storedRoleId = storedRoleIds != null && i < storedRoleIds.Count ? storedRoleIds[i] : (int?)null;
                var userId = stage.UserId;
                stage.UserId = ValidatePerson(
                    stage.RoleId,
                    storedRoleId,
                    userId,
                    organizationId,
                    findUser,
                    message => addError(key, label + ": " + message));
            }
        }

        /// <summary>
        /// Validates the PO-raiser role/person. Role must exist in the org; person rules as for stages.
        /// Returns the normalized person id (null = any user in role). Clears the person when no role is set.
        /// </summary>
        public static string ValidatePurchaseOrderRaiser(
            int? roleId,
            string userId,
            int? storedRoleId,
            int organizationId,
            ICollection<int> organizationRoleIds,
            Func<string, ApproverUserRecord> findUser,
            Action<string, string> addError)
        {
            if (!roleId.HasValue || roleId.Value <= 0)
            {
                return null;
            }

            if (organizationRoleIds != null && !organizationRoleIds.Contains(roleId.Value))
            {
                if (addError != null)
                {
                    addError("PurchaseOrderRaiserRoleId", "Purchase order raised by: the selected role does not exist in this organisation.");
                }

                return null;
            }

            return ValidatePerson(
                roleId,
                storedRoleId,
                userId,
                organizationId,
                findUser,
                message =>
                {
                    if (addError != null)
                    {
                        addError("PurchaseOrderRaiserUserId", "Purchase order raised by: " + message);
                    }
                });
        }

        private static string ValidatePerson(
            int? roleId,
            int? storedRoleId,
            string userId,
            int organizationId,
            Func<string, ApproverUserRecord> findUser,
            Action<string> addError)
        {
            var normalized = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            if (normalized == null)
            {
                return null;
            }

            if (!roleId.HasValue || roleId.Value <= 0)
            {
                // No role: a person cannot be set on its own.
                return null;
            }

            var user = findUser == null ? null : findUser(normalized);
            if (user == null || !user.OrganizationId.HasValue || user.OrganizationId.Value != organizationId)
            {
                addError("the selected person is not a user in this organisation.");
                return null;
            }

            if (!user.IsActive)
            {
                addError("the selected person is inactive. Choose an active user or " + AnyUserInRoleLabel + ".");
                return null;
            }

            if (!user.RoleId.HasValue || user.RoleId.Value != roleId.Value)
            {
                var roleChanged = !storedRoleId.HasValue || storedRoleId.Value != roleId.Value;
                if (roleChanged)
                {
                    // Role changed in this save: a person who no longer matches is cleared, not an error.
                    return null;
                }

                addError("the selected person does not hold the selected role.");
                return null;
            }

            return user.Id ?? normalized;
        }

        /// <summary>
        /// Submission snapshot: keep a configured person only while they are still active and hold the stage
        /// role; otherwise that stage falls back to "any user in role" so a requisition never gets stuck.
        /// </summary>
        public static IList<string> SanitizeForSnapshot(
            IList<int> stageRoleIds,
            IList<string> stageUserIds,
            Func<string, ApproverUserRecord> findUser)
        {
            var result = new List<string>();
            var roles = stageRoleIds ?? new List<int>();
            for (var i = 0; i < roles.Count; i++)
            {
                var userId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, i + 1);
                if (userId == null)
                {
                    result.Add(null);
                    continue;
                }

                var user = findUser == null ? null : findUser(userId);
                result.Add(user != null && user.IsActive && user.RoleId.HasValue && user.RoleId.Value == roles[i]
                    ? userId
                    : null);
            }

            return result;
        }

        /// <summary>
        /// The auto-select rule used by the picker (mirrored in approval-stage-person.js): exactly one active
        /// role holder = that user; zero or several = null ("Any user in role").
        /// </summary>
        public static string ResolveAutoSelectUserId(IEnumerable<ApproverUserRecord> users, int? roleId)
        {
            if (!roleId.HasValue)
            {
                return null;
            }

            var holders = (users ?? Enumerable.Empty<ApproverUserRecord>())
                .Where(x => x != null && x.IsActive && x.RoleId == roleId && !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => x.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return holders.Count == 1 ? holders[0] : null;
        }

        public static string BuildPersonLabel(string email, string firstName, string lastName, string fallbackId)
        {
            var name = ((firstName ?? string.Empty) + " " + (lastName ?? string.Empty)).Trim();
            var mail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            if (mail == null)
            {
                return string.IsNullOrWhiteSpace(name) ? fallbackId : name;
            }

            return string.IsNullOrWhiteSpace(name) ? mail : mail + " (" + name + ")";
        }
    }
}
