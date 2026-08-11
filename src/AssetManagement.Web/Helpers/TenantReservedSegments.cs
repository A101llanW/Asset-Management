using System;
using System.Collections.Generic;

namespace AssetManagement.Web.Helpers
{
    internal static class TenantReservedSegments
    {
        private static readonly HashSet<string> Segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Account",
            "api",
            "AssetCategories",
            "AssetRequests",
            "AssetScan",
            "Assets",
            "AssetSubTypes",
            "AssetTypes",
            "Assignments",
            "AuditLogs",
            "bundles",
            "Captcha",
            "Claims",
            "Content",
            "Custodian",
            "Dashboard",
            "Departments",
            "Documents",
            "favicon.ico",
            "Home",
            "Incidents",
            "InsurancePolicies",
            "Maintenance",
            "Notifications",
            "PendingApprovals",
            "Permissions",
            "Platform",
            "PurchaseRequests",
            "Purchases",
            "Reports",
            "Returns",
            "Roles",
            "Scripts",
            "Search",
            "SecurityLogs",
            "Settings",
            "Suppliers",
            "Transfers",
            "Users"
        };

        public static bool IsReserved(string segment)
        {
            return !string.IsNullOrWhiteSpace(segment) && Segments.Contains(segment.Trim());
        }
    }
}
