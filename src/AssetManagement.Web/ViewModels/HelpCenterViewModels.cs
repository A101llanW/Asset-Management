using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Web.ViewModels
{
    /// <summary>
    /// Hidden how-to video page (/{tenant}/HelpVideos, feat/hidden-help-videos). Catalog ported from the old master
    /// Home/Help page (64fdcbd) without its pre-login / platform parts; uploaded videos (table HelpVideo) are attached
    /// per topic slug. No menu or button links here on purpose (Allan, 2026-10-28: reachable by direct URL only).
    /// </summary>
    public class HelpCenterVm
    {
        public IList<HelpVideoGroupVm> Groups { get; set; }

        /// <summary>Settings.Manage: may upload, replace and delete videos.</summary>
        public bool CanManage { get; set; }

        /// <summary>Topic choices for the upload form (catalog order).</summary>
        public IList<HelpVideoTopicOptionVm> TopicOptions { get; set; }

        public int VideoCount { get; set; }

        public int ChunkSizeBytes { get; set; }

        public long MaxFileBytes { get; set; }
    }

    public class HelpVideoTopicOptionVm
    {
        public string Slug { get; set; }

        public string Label { get; set; }

        public bool HasVideo { get; set; }
    }

    public class HelpVideoGroupVm
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Summary { get; set; }

        public IList<HelpVideoTopicVm> Topics { get; set; }
    }

    public class HelpVideoTopicVm
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Outline { get; set; }

        /// <summary>The uploaded video for this topic (tenant row wins over an all-tenant row), or null.</summary>
        public HelpVideoInfo Video { get; set; }

        public bool HasVideo
        {
            get { return Video != null; }
        }
    }

    public static class HelpVideoCatalog
    {
        public const string MoreVideosGroupId = "more-videos";

        public static IList<HelpVideoGroupVm> BuildGroups()
        {
            return new List<HelpVideoGroupVm>
            {
                BuildGettingStarted(),
                BuildAssetsAndCustody(),
                BuildWorkflow(),
                BuildProcurement(),
                BuildRisk(),
                BuildFinanceAndReports(),
                BuildOrganization(),
                BuildAdministration()
            };
        }

        /// <summary>
        /// Catalog plus the uploaded videos. A tenant's own video wins over an all-tenant one for the same topic.
        /// Videos whose topic is not in the catalog are listed under "More videos".
        /// </summary>
        public static HelpCenterVm Build(IEnumerable<HelpVideoInfo> videos, bool canManage)
        {
            var bySlug = new Dictionary<string, HelpVideoInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var video in (videos ?? Enumerable.Empty<HelpVideoInfo>()).Where(v => v != null && !string.IsNullOrWhiteSpace(v.TopicSlug)))
            {
                HelpVideoInfo current;
                if (!bySlug.TryGetValue(video.TopicSlug, out current) || (current.IsGlobal && !video.IsGlobal))
                {
                    bySlug[video.TopicSlug] = video;
                }
            }

            var groups = BuildGroups();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var options = new List<HelpVideoTopicOptionVm>();
            foreach (var group in groups)
            {
                foreach (var topic in group.Topics)
                {
                    known.Add(topic.Id);
                    HelpVideoInfo video;
                    if (bySlug.TryGetValue(topic.Id, out video))
                    {
                        topic.Video = video;
                    }

                    options.Add(new HelpVideoTopicOptionVm
                    {
                        Slug = topic.Id,
                        Label = group.Title + ": " + topic.Title,
                        HasVideo = topic.HasVideo
                    });
                }
            }

            var extra = bySlug.Values
                .Where(v => !known.Contains(v.TopicSlug))
                .OrderBy(v => v.Title, StringComparer.OrdinalIgnoreCase)
                .Select(v => new HelpVideoTopicVm { Id = v.TopicSlug, Title = v.Title, Outline = null, Video = v })
                .ToList();
            if (extra.Count > 0)
            {
                groups.Add(new HelpVideoGroupVm
                {
                    Id = MoreVideosGroupId,
                    Title = "More videos",
                    Summary = "Other uploaded how-to videos.",
                    Topics = extra
                });
            }

            return new HelpCenterVm
            {
                Groups = groups,
                CanManage = canManage,
                TopicOptions = options,
                VideoCount = bySlug.Count,
                ChunkSizeBytes = AssetManagement.Application.Helpers.HelpVideoRules.ChunkSizeBytes,
                MaxFileBytes = AssetManagement.Application.Helpers.HelpVideoRules.MaxFileBytes
            };
        }

        private static HelpVideoGroupVm BuildGettingStarted()
        {
            return Group(
                "getting-started",
                "Getting started",
                "Sign in, secure your account, and find your way around the module.",
                Topic("sign-in", "Sign in", "Open the sign-in page, enter your email and password, and continue to your dashboard."),
                Topic("mfa", "MFA", "Enable email two-step verification when prompted, then enter the code at sign-in."),
                Topic("password-reset", "Password reset", "Use Forgot password on the login page, open the email link, and set a new password."),
                Topic("profile", "Profile", "Open Profile to update name and phone, change email, or change your password."),
                Topic("navigation", "Navigation and dashboard", "Use the sidebar modules, header search, and dashboard KPIs and alert badges. Open Notifications for the alert list and unread count."));
        }

        private static HelpVideoGroupVm BuildAssetsAndCustody()
        {
            return Group(
                "assets-custody",
                "Assets and custody",
                "Register assets, move custody, and walk the full lifecycle.",
                Topic("asset-lifecycle", "Asset lifecycle", "Walk one asset from register through assign, incident, maintenance, and transfer, then review the Audit Trail tab. Use the matching how-tos below for each step."),
                Topic("scan-search", "Scan and search", "Look up an asset from the scan page, header search, or a printed QR or barcode, including the public scan portal."),
                Topic("create-edit", "Create and edit assets", "Add an asset with category, type, department, financial, and tax details, then maintain the record."),
                Topic("import", "Import assets", "Download the import template, fill rows, and upload a bulk register."),
                Topic("assignments", "Assignments", "Assign an asset to a custodian and review the assignment list. Use My Assets or Department Assets for the filtered custody views."),
                Topic("transfers", "Transfers and approval", "Start the transfer wizard from the asset. When transfer approval is on, a second user must approve it from Pending Approvals or the asset record."),
                Topic("returns", "Returns", "Request a return from My Assets, then complete the return on the asset so operations can acknowledge the handback."),
                Topic("labels", "Asset labels and printing", "Generate an asset QR or barcode label and print it to a Zebra ZD421 or the browser printer."),
                Topic("documents", "Documents and attachments", "Upload supporting files on an asset and keep the document tab current."),
                Topic("disposal", "Disposal", "Request disposal, follow the approval path, and close the asset lifecycle."),
                Topic("bulk-register", "Bulk actions and grouped register", "On the asset register, switch Grouped or Flat view. Expand a group to select units, then bulk-move department, update status, or mark under maintenance."));
        }

        private static HelpVideoGroupVm BuildWorkflow()
        {
            return Group(
                "workflow",
                "Workflow",
                "Raise requests and act on items waiting for you.",
                Topic("asset-requests", "Asset requests", "Submit a request for an asset and track its status."),
                Topic("pending-approvals", "Pending approvals", "Open the approvals queue, act on your stage, and see aging items."));
        }

        private static HelpVideoGroupVm BuildProcurement()
        {
            return Group(
                "procurement",
                "Procurement",
                "From requisition through purchase order, receipt, and suppliers.",
                Topic("hod-start-requisition", "Start a requisition (Head of Department)", "As Head of Department, open Requisitions, add the item lines for your department and submit the request for approval."),
                Topic("hod-approve-requisition", "Approve a requisition (Head of Department)", "Open Pending Approvals, review a requisition raised in your department and approve or reject it with a comment."),
                Topic("requisitions", "Requisitions", "Create a purchase request, pick a target asset when needed, and send it for approval."),
                Topic("purchases", "Purchases", "Create and manage a purchase record: supplier, purchase order, invoice, and totals."),
                Topic("receiving", "Receiving", "Open Receive on the purchase and enter a serial for each remaining unit in the receive wizard."),
                Topic("suppliers", "Suppliers and catalogs", "Maintain suppliers and choose catalog items when building a purchase."));
        }

        private static HelpVideoGroupVm BuildRisk()
        {
            return Group(
                "risk",
                "Risk and incidents",
                "Record incidents, claims, insurance, and maintenance against assets.",
                Topic("incidents", "Incidents", "Log an incident on an asset, update its status, and link follow-up work."),
                Topic("claims", "Claims", "Open an insurance or loss claim on the asset; linking a related incident is optional."),
                Topic("insurance", "Insurance policies", "Add a policy from the asset's Insurance / Claims tab and watch renewal windows."),
                Topic("maintenance", "Maintenance tickets", "Open a repair ticket, complete work, and return the asset to service."));
        }

        private static HelpVideoGroupVm BuildFinanceAndReports()
        {
            return Group(
                "finance-reports",
                "Finance and reports",
                "Run operational and financial reports, then export the results.",
                Topic("reports", "Run and export a report", "Open the Reports hub, pick a report type, apply period or department filters, then preview and export to CSV."));
        }

        private static HelpVideoGroupVm BuildOrganization()
        {
            return Group(
                "organization",
                "Organization",
                "Set up the structure people and assets belong to.",
                Topic("departments", "Departments", "Create departments and keep users and assets scoped correctly. You can also add a sub-unit, grade streams, or bulk grades 1 to 6."),
                Topic("taxonomy", "Categories, types, and subtypes", "Build the asset taxonomy and set useful-life defaults used for depreciation."),
                Topic("users", "Users and invitations", "Invite or create users, assign departments, and complete registration from an invite."),
                Topic("roles", "Roles and permissions", "Edit a role, use the permission matrix, and save a role template."));
        }

        private static HelpVideoGroupVm BuildAdministration()
        {
            return Group(
                "administration",
                "Administration",
                "Operate logs and tenant settings.",
                Topic("logs", "Logs", "Filter Audit Logs for operational history or Security Logs for sign-in and MFA events, then export when needed."),
                Topic("settings", "Settings", "Set currency, notification thresholds, the approval matrix for requisitions, transfers, and disposals, attachments path, and the label printer."));
        }

        private static HelpVideoGroupVm Group(string id, string title, string summary, params HelpVideoTopicVm[] topics)
        {
            return new HelpVideoGroupVm
            {
                Id = id,
                Title = title,
                Summary = summary,
                Topics = new List<HelpVideoTopicVm>(topics)
            };
        }

        private static HelpVideoTopicVm Topic(string id, string title, string outline)
        {
            return new HelpVideoTopicVm
            {
                Id = id,
                Title = title,
                Outline = outline
            };
        }
    }
}
