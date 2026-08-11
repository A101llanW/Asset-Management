using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Filters asset audit trail views to meaningful business lifecycle events,
    /// excluding HTTP controller hits and low-value update noise.
    /// </summary>
    public static class AssetAuditTrailFilterHelper
    {
        private static readonly HashSet<string> AllowedActions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets.Create",
                "Assets.Edit",
                "Assets.Delete",
                "Assets.Assign",
                "Assets.Return",
                "Assets.Transfer",
                "Assets.UpdateStatus",
                "Assets.RelocateClass",
                "Assets.AcknowledgeReceipt",
                "Assets.RequestReturn",
                "Assets.RequestDisposal",
                "Assets.ApproveDisposal",
                "Assets.Import",
                "Assets.Bulk.department",
                "Assets.Bulk.status",
                "Assets.Bulk.maintenance",
                "Incidents.Create",
                "Maintenance.Create",
                "Maintenance.Complete",
                "Claims.Create",
                "Documents.Upload",
                "Documents.Delete",
                "Documents.Requirement.Create",
                "Documents.Requirement.Fulfill",
                "Documents.Requirement.Clear",
                "Insurance.Create",
                "Insurance.Update",
                "Insurance.Delete",
                "AssetRequests.Submit",
                "AssetRequests.Approve",
                "AssetRequests.Reject",
                "AssetRequests.Fulfill",
                "Purchases.Receive"
            };

        public static bool IsBusinessEvent(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return false;
            }

            var trimmed = action.Trim();
            if (trimmed.StartsWith("HTTP.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return AllowedActions.Contains(trimmed);
        }

        public static IList<AuditLogVm> FilterBusinessEvents(IEnumerable<AuditLogVm> logs)
        {
            if (logs == null)
            {
                return new List<AuditLogVm>();
            }

            return logs.Where(x => IsBusinessEvent(x.Action)).ToList();
        }
    }
}
