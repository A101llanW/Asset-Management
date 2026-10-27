namespace AssetSuite.Web.Infrastructure;

public static class TenantReservedSegments
{
    private static readonly HashSet<string> Segments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Account", "api", "AssetCategories", "AssetRequests", "AssetScan", "Assets", "AssetSubTypes",
        "AssetTypes", "Assignments", "AuditLogs", "bundles", "Captcha", "Claims", "Content", "Custodian",
        "Dashboard", "DatabaseConsole", "Departments", "Documents", "favicon.ico", "Home", "Incidents",
        "InsurancePolicies", "Maintenance", "Notifications", "PendingApprovals", "Permissions", "Platform",
        "PurchaseRequests", "Purchases", "Reports", "Returns", "Roles", "Scripts", "Search", "SecurityLogs",
        "Settings", "Sys", "Suppliers", "Transfers", "Users"
    };

    public static bool IsReserved(string segment) =>
        !string.IsNullOrWhiteSpace(segment) && Segments.Contains(segment.Trim());
}
