using System;

using System.Collections.Generic;

using System.Text.RegularExpressions;



namespace AssetManagement.Application.Helpers

{

    public static class AuditDisplayLabelHelper

    {

        private static readonly Dictionary<string, string> ActionLabels =

            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

            {

                { "Incidents.Create", "Incident reported" },

                { "Incidents.Edit", "Incident updated" },

                { "Maintenance.Create", "Maintenance ticket opened" },

                { "Maintenance.Complete", "Maintenance completed" },

                { "Claims.Create", "Insurance claim filed" },

                { "Claims.Edit", "Insurance claim updated" },

                { "Insurance.Create", "Insurance policy added" },

                { "Insurance.Update", "Insurance policy updated" },

                { "Insurance.Delete", "Insurance policy removed" },

                { "Assets.Create", "Asset registered" },

                { "Assets.Edit", "Asset details updated" },

                { "Assets.Delete", "Asset removed" },

                { "Assets.Assign", "Asset assigned" },

                { "Assets.Return", "Asset returned" },

                { "Assets.Transfer", "Asset transfer recorded" },

                { "Assets.UpdateStatus", "Asset status changed" },

                { "Assets.AcknowledgeReceipt", "Assignment receipt acknowledged" },

                { "Assets.RequestReturn", "Return requested by custodian" },

                { "Assets.RequestDisposal", "Disposal requested" },

                { "Assets.ApproveDisposal", "Disposal approval updated" },

                { "Assets.Import", "Assets imported" },

                { "Assets.Bulk.department", "Bulk department update" },

                { "Assets.Bulk.status", "Bulk status update" },

                { "Assets.Bulk.maintenance", "Bulk maintenance update" },

                { "Documents.Upload", "Document uploaded" },

                { "Documents.Delete", "Document removed" },

                { "AssetRequests.Submit", "Asset request submitted" },

                { "AssetRequests.Approve", "Asset request approved" },

                { "AssetRequests.Reject", "Asset request rejected" },

                { "AssetRequests.Fulfill", "Asset request fulfilled" },

                { "Purchases.Request", "Purchase request submitted" },

                { "Purchases.Approve", "Purchase request approval updated" },

                { "Roles.Create", "Role created" },

                { "Roles.Edit", "Role updated" },

                { "Permissions.Assign", "Role permissions updated" },

                { "Users.AssignRole", "User role updated" },

                { "Webhooks.Register", "Webhook registered" },

                { "Webhooks.Deactivate", "Webhook deactivated" },

                { "Api.Assets.BreakGlassStatus", "Asset status changed (admin override)" },

                { "Approval.SelfApprove", "Approval self-approved" },

                { "Approval.UserBypass", "Approval user bypass used" },

                { "Approval.RoleBypass", "Approval role bypass used" },

                { "ORGANIZATION_CREATED", "Organization created" },

                { "LICENSE_RENEWED", "License renewed" },

                { "LICENSE_PAUSED", "License paused" },

                { "LICENSE_RESUMED", "License resumed" },

                { "LICENSE_REVOKED", "License revoked" },

                { "LICENSE_EXTENDED", "License extended" },

                { "LICENSE_PLAN_CHANGED", "License plan changed" },

                { "LICENSE_EXPIRED", "License expired" },

                { "IMPERSONATION_APPROVED", "Support access approved" },

                { "IMPERSONATION_REJECTED", "Support access rejected" },

                { "IMPERSONATION_CANCELLED", "Support access cancelled" },

                { "IMPERSONATION_STOP", "Support access ended" },

                { "IMPERSONATION_RESUME", "Support access resumed" },

                { "IMPERSONATION_EXPIRED", "Support access expired" }

            };



        private static readonly Dictionary<string, string> EntityTypeLabels =

            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

            {

                { "Asset", "Asset" },

                { "AssetIncident", "Incident" },

                { "AssetMaintenanceRecord", "Maintenance ticket" },

                { "InsuranceClaim", "Insurance claim" },

                { "InsurancePolicy", "Insurance policy" },

                { "AssetAssignment", "Assignment" },

                { "AssetTransfer", "Transfer" },

                { "AssetReturn", "Return" },

                { "AssetDocument", "Document" },

                { "AssetRequest", "Asset request" },

                { "PurchaseRequest", "Purchase request" },

                { "DisposalRecord", "Disposal request" },

                { "Role", "Role" },

                { "RolePermission", "Role permissions" },

                { "WebhookSubscription", "Webhook" },

                { "ApprovalWorkflow", "Approval workflow" },

                { "Organization", "Organization" },

                { "OrganizationLicense", "Organization license" },

                { "ApplicationUser", "User account" },

                { "ImpersonationRequest", "Support access request" }

            };



        public static string FormatAction(string action)

        {

            if (string.IsNullOrWhiteSpace(action))

            {

                return string.Empty;

            }



            var trimmed = action.Trim();

            string mapped;

            if (ActionLabels.TryGetValue(trimmed, out mapped))

            {

                return mapped;

            }



            if (trimmed.IndexOf('_') >= 0)

            {

                return HumanizeUnderscore(trimmed);

            }



            if (trimmed.IndexOf('.') >= 0)

            {

                return HumanizeDottedAction(trimmed);

            }



            return trimmed;

        }



        public static string FormatEntityType(string entityType)

        {

            if (string.IsNullOrWhiteSpace(entityType))

            {

                return string.Empty;

            }



            var trimmed = entityType.Trim();

            string mapped;

            if (EntityTypeLabels.TryGetValue(trimmed, out mapped))

            {

                return mapped;

            }



            return HumanizePascalCase(trimmed);

        }



        public static void ApplyDisplayLabels(AuditLogDisplayItem item)

        {

            if (item == null)

            {

                return;

            }



            item.ActionLabel = FormatAction(item.Action);

            item.EntityTypeLabel = FormatEntityType(item.EntityType);

        }



        private static string HumanizeDottedAction(string action)

        {

            var parts = action.Split('.');

            if (parts.Length < 2)

            {

                return action;

            }



            var verb = parts[parts.Length - 1];

            var moduleParts = new string[parts.Length - 1];

            Array.Copy(parts, moduleParts, parts.Length - 1);

            var module = string.Join(" ", moduleParts);

            module = SingularizeModule(module.Replace('.', ' '));

            return module + " " + HumanizeVerb(verb);

        }



        private static string HumanizeUnderscore(string action)

        {

            var words = action.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)

            {

                return action;

            }



            for (var i = 0; i < words.Length; i++)

            {

                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();

            }



            return string.Join(" ", words);

        }



        private static string HumanizePascalCase(string value)

        {

            if (string.IsNullOrWhiteSpace(value))

            {

                return string.Empty;

            }



            return Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");

        }



        private static string SingularizeModule(string module)

        {

            if (string.IsNullOrWhiteSpace(module))

            {

                return module;

            }



            if (module.EndsWith("ies", StringComparison.OrdinalIgnoreCase))

            {

                return module.Substring(0, module.Length - 3) + "y";

            }



            if (module.EndsWith("s", StringComparison.OrdinalIgnoreCase) && module.Length > 1)

            {

                return module.Substring(0, module.Length - 1);

            }



            return module;

        }



        private static string HumanizeVerb(string verb)

        {

            switch (verb.ToLowerInvariant())

            {

                case "create":

                    return "created";

                case "edit":

                case "update":

                    return "updated";

                case "delete":

                    return "removed";

                case "complete":

                    return "completed";

                case "assign":

                    return "assigned";

                case "submit":

                    return "submitted";

                case "approve":

                    return "approved";

                case "reject":

                    return "rejected";

                case "fulfill":

                    return "fulfilled";

                case "register":

                    return "registered";

                case "deactivate":

                    return "deactivated";

                case "upload":

                    return "uploaded";

                case "import":

                    return "imported";

                default:

                    return verb.ToLowerInvariant();

            }

        }

    }



    public interface AuditLogDisplayItem

    {

        string Action { get; set; }



        string EntityType { get; set; }



        string ActionLabel { get; set; }



        string EntityTypeLabel { get; set; }

    }

}


