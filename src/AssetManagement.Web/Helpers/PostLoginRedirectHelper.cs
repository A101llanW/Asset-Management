using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;

namespace AssetManagement.Web.Helpers
{
    public static class PostLoginRedirectHelper
    {
        private static readonly DestinationCandidate[] DefaultDestinations = new[]
        {
            new DestinationCandidate("Reports.View", "Dashboard", "Index"),
            new DestinationCandidate("Assets.View", "Assets", "Index"),
            new DestinationCandidate("Purchases.View", "Purchases", "Index"),
            new DestinationCandidate("Incidents.View", "Incidents", "Index"),
            new DestinationCandidate("Claims.View", "Claims", "Index"),
            new DestinationCandidate("Departments.View", "Departments", "Index"),
            new DestinationCandidate("Suppliers.View", "Suppliers", "Index"),
            new DestinationCandidate("Users.View", "Users", "Index"),
            new DestinationCandidate("Roles.View", "Roles", "Index"),
            new DestinationCandidate("AuditLogs.View", "AuditLogs", "Index"),
            new DestinationCandidate("Settings.Manage", "Settings", "Index")
        };

        private static readonly Dictionary<string, string> ControllerViewPermissions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Dashboard", "Reports.View" },
                { "Assets", "Assets.View" },
                { "Purchases", "Purchases.View" },
                { "PurchaseRequests", "Purchases.View" },
                { "Incidents", "Incidents.View" },
                { "Claims", "Claims.View" },
                { "Departments", "Departments.View" },
                { "Suppliers", "Suppliers.View" },
                { "Users", "Users.View" },
                { "Roles", "Roles.View" },
                { "AuditLogs", "AuditLogs.View" },
                { "SecurityLogs", "SecurityLogs.View" },
                { "Settings", "Settings.Manage" },
                { "Reports", "Reports.View" },
                { "Notifications", "Reports.View" },
                { "Depreciation", "Depreciation.View" },
                { "Custodian", "Assets.View" },
                { "AssetScan", "Assets.View" },
                { "AssetRequests", "Assets.Request" },
                { "PendingApprovals", "Assets.View" },
                { "Search", "Assets.View" },
                { "Maintenance", "Assets.View" },
                { "Assignments", "Assets.View" },
                { "Transfers", "Assets.Transfer" },
                { "Returns", "Assets.Return" },
                { "Documents", "Documents.View" }
            };

        public static PostLoginDestination ResolveDefaultDestination(
            IAuthorizationService authorizationService,
            string userId,
            bool isPlatformAdmin)
        {
            if (isPlatformAdmin)
            {
                return PostLoginDestination.ForPlatform("Organizations", "Index");
            }

            if (authorizationService == null || string.IsNullOrWhiteSpace(userId))
            {
                return PostLoginDestination.ForTenant("Account", "Landing");
            }

            foreach (var candidate in DefaultDestinations)
            {
                if (authorizationService.HasPermission(userId, candidate.Permission))
                {
                    return PostLoginDestination.ForTenant(candidate.Controller, candidate.Action);
                }
            }

            return PostLoginDestination.ForTenant("Account", "Profile");
        }

        public static bool CanAccessReturnPath(
            IAuthorizationService authorizationService,
            string userId,
            string returnPath)
        {
            if (authorizationService == null ||
                string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(returnPath) ||
                LocalReturnUrlHelper.IsDefaultTenantLandingPath(returnPath))
            {
                return false;
            }

            var segments = returnPath.Split('?')[0].Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            var controller = segments[1];
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string permission;
            if (!ControllerViewPermissions.TryGetValue(controller, out permission))
            {
                return false;
            }

            return authorizationService.HasPermission(userId, permission);
        }

        public sealed class PostLoginDestination
        {
            public string Area { get; private set; }
            public string Controller { get; private set; }
            public string Action { get; private set; }

            public static PostLoginDestination ForTenant(string controller, string action)
            {
                return new PostLoginDestination
                {
                    Controller = controller,
                    Action = action
                };
            }

            public static PostLoginDestination ForPlatform(string controller, string action)
            {
                return new PostLoginDestination
                {
                    Area = "Platform",
                    Controller = controller,
                    Action = action
                };
            }
        }

        private sealed class DestinationCandidate
        {
            public DestinationCandidate(string permission, string controller, string action)
            {
                Permission = permission;
                Controller = controller;
                Action = action;
            }

            public string Permission { get; private set; }
            public string Controller { get; private set; }
            public string Action { get; private set; }
        }
    }
}
