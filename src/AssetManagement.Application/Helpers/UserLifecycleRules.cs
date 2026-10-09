using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    public enum UserLifecycleAction
    {
        Deactivate,
        Reactivate,
        Delete
    }

    /// <summary>
    /// rel1009g guards for deactivate / reactivate / delete user (pure, unit-tested):
    /// nobody acts on their own account, and the organization always keeps at least one active Company Admin.
    /// Hard delete additionally requires no linked history (blockers), otherwise deactivate is suggested.
    /// </summary>
    public static class UserLifecycleRules
    {
        public const string SelfMessage = "You cannot deactivate or delete your own account.";
        public const string LastAdminMessage = "This is the last active Company Admin of the organization. Make another user Company Admin first.";

        /// <summary>Returns null when allowed, otherwise the reason.</summary>
        public static string Evaluate(
            UserLifecycleAction action,
            string actorUserId,
            string targetUserId,
            bool targetIsActive,
            bool targetIsCompanyAdmin,
            int otherActiveCompanyAdmins)
        {
            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                return "User not found.";
            }

            if (action == UserLifecycleAction.Reactivate)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(actorUserId)
                && string.Equals(actorUserId.Trim(), targetUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return SelfMessage;
            }

            if (targetIsActive && targetIsCompanyAdmin && otherActiveCompanyAdmins <= 0)
            {
                return LastAdminMessage;
            }

            return null;
        }

        /// <summary>"Cannot delete ... (3 asset assignments, 12 audit log entries). Deactivate the user instead."</summary>
        public static string BuildBlockedMessage(IEnumerable<KeyValuePair<string, int>> blockers)
        {
            var parts = (blockers ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .Where(x => x.Value > 0 && !string.IsNullOrWhiteSpace(x.Key))
                .Select(x => x.Value + " " + x.Key)
                .ToList();
            if (parts.Count == 0)
            {
                return null;
            }

            return "This user cannot be deleted because they have linked records (" + string.Join(", ", parts)
                + "). Deactivate the user instead: they will no longer be able to sign in and the history is kept.";
        }

        /// <summary>Readable label for a blocking column, e.g. AssetAssignment.ToUserId -> "asset assignment record(s)".</summary>
        public static string DescribeColumn(string table, string column)
        {
            var t = (table ?? string.Empty).Trim();
            switch (t)
            {
                case "AuditLog":
                    return "audit log entries";
                case "LoginAttempts":
                    return "sign-in records";
                case "Asset":
                    return string.Equals(column, "CurrentCustodianId", StringComparison.OrdinalIgnoreCase)
                        ? "assets in custody"
                        : "asset approval-stage references";
                case "AssetAssignment":
                    return "asset assignment records";
                case "AssetCustodyEvent":
                case "AssetMovement":
                    return "custody/movement history records";
                case "AssetRequest":
                case "PurchaseRequest":
                case "AssetTransfer":
                case "DisposalRecord":
                    return "request/approval records (" + t + ")";
                case "PurchaseApprovalAction":
                case "TransferApprovalAction":
                case "DisposalApprovalAction":
                case "DepartmentApprovalStage":
                    return "approval records (" + t + ")";
                case "Department":
                    return "department person-in-charge / approver references";
                default:
                    return t + "." + column + " references";
            }
        }
    }
}
