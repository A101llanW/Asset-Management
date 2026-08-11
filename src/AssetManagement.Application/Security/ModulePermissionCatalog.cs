using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Security
{
    public sealed class ModulePermissionDefinition
    {
        public ModulePermissionDefinition(string key, string displayName, string description, IEnumerable<string> permissionCodes)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
            PermissionCodes = permissionCodes.ToList().AsReadOnly();
        }

        public string Key { get; private set; }

        public string DisplayName { get; private set; }

        public string Description { get; private set; }

        public IList<string> PermissionCodes { get; private set; }
    }

    public static class ModulePermissionCatalog
    {
        public const string Assets = "Assets";
        public const string Purchases = "Purchases";
        public const string Users = "Users";
        public const string Roles = "Roles";
        public const string Departments = "Departments";
        public const string Suppliers = "Suppliers";
        public const string Reports = "Reports";
        public const string SecurityLogs = "SecurityLogs";
        public const string Settings = "Settings";
        public const string Incidents = "Incidents";
        public const string Financials = "Financials";
        public const string Documents = "Documents";

        private static readonly IList<ModulePermissionDefinition> Modules = new List<ModulePermissionDefinition>
        {
            new ModulePermissionDefinition(Assets, "Assets", "Asset inventory, custody, transfers, requests, and disposals.",
                new[] { "Assets.View", "Assets.Create", "Assets.Edit", "Assets.Delete", "Assets.Assign", "Assets.Transfer", "Assets.Return", "Assets.Receive", "Assets.Dispose", "Assets.ApproveDisposal", "Assets.Request", "Assets.Request.Approve" }),
            new ModulePermissionDefinition(Purchases, "Purchases", "Requisitions, purchase orders, and procurement approvals.",
                new[] { "Purchases.View", "Purchases.Create", "Purchases.CreateForAnyDepartment", "Purchases.Edit", "Purchases.Approve" }),
            new ModulePermissionDefinition(Users, "User Management", "Tenant user accounts, department assignment, and role assignment from user details.",
                new[] { "Users.View", "Users.ViewDepartment", "Users.Create", "Users.Edit", "Users.Delete" }),
            new ModulePermissionDefinition(Roles, "Roles & Permissions", "Role templates, permission assignment, and approval-stage role choices.",
                new[] { "Roles.View", "Roles.Create", "Roles.Edit", "Roles.Delete", "Permissions.Assign" }),
            new ModulePermissionDefinition(Departments, "Departments", "Department structure and ownership.",
                new[] { "Departments.View", "Departments.Create", "Departments.Edit", "Departments.Delete" }),
            new ModulePermissionDefinition(Suppliers, "Suppliers", "Supplier profiles, catalog items, and procurement links.",
                new[] { "Suppliers.View", "Suppliers.Create", "Suppliers.Edit", "Suppliers.Delete" }),
            new ModulePermissionDefinition(Documents, "Documents", "Asset attachments and document upload or download.",
                new[] { "Documents.View", "Documents.Download", "Documents.Upload", "Documents.Delete" }),
            new ModulePermissionDefinition(Reports, "Reports", "Operational dashboards and exported reports.",
                new[] { "Reports.View", "Reports.Export", "AuditLogs.View" }),
            new ModulePermissionDefinition(SecurityLogs, "Security Logs", "Login attempts and security events.",
                new[] { "SecurityLogs.View", "AuditLogs.View" }),
            new ModulePermissionDefinition(Settings, "Settings", "Workflow defaults, approval matrix, and organization configuration.",
                new[] { "Settings.Manage" }),
            new ModulePermissionDefinition(Incidents, "Incidents & Claims", "Incidents, insurance policies, and claims.",
                new[] { "Incidents.View", "Incidents.Create", "Incidents.Edit", "Claims.View", "Claims.Create", "Claims.Edit", "Insurance.Manage" }),
            new ModulePermissionDefinition(Financials, "Financials", "Financial records, depreciation, and cost reporting.",
                new[] { "Financials.View", "Financials.Edit", "Depreciation.View", "Depreciation.Manage" })
        }.AsReadOnly();

        public static IList<ModulePermissionDefinition> All
        {
            get { return Modules; }
        }

        public static ModulePermissionDefinition Find(string moduleKey)
        {
            return Modules.FirstOrDefault(m => string.Equals(m.Key, moduleKey, StringComparison.OrdinalIgnoreCase));
        }

        public static string ResolveModule(string controllerName)
        {
            controllerName = controllerName ?? string.Empty;
            switch (controllerName.ToLowerInvariant())
            {
                case "assets":
                case "assetscan":
                case "custodian":
                case "maintenance":
                case "transfers":
                case "returns":
                case "disposals":
                case "receivings":
                case "assetrequests":
                    return Assets;
                case "purchaserequests":
                case "purchases":
                    return Purchases;
                case "users":
                    return Users;
                case "roles":
                    return Roles;
                case "departments":
                    return Departments;
                case "suppliers":
                    return Suppliers;
                case "reports":
                case "dashboard":
                    return Reports;
                case "securitylogs":
                    return SecurityLogs;
                case "settings":
                case "approvalworkflow":
                    return Settings;
                case "incidents":
                case "insurancepolicies":
                case "claims":
                    return Incidents;
                case "financials":
                case "depreciation":
                    return Financials;
                case "documents":
                    return Documents;
                default:
                    return null;
            }
        }

        public static IEnumerable<string> PermissionCodesForModule(string moduleKey)
        {
            var module = Find(moduleKey);
            return module == null ? Enumerable.Empty<string>() : module.PermissionCodes;
        }
    }
}
