using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [TenantAuthorize]
    public class PendingApprovalsController : BaseController
    {
        private readonly IPendingApprovalQueryService _pendingApprovalQuery;

        public PendingApprovalsController()
        {
            _pendingApprovalQuery = BuildPendingApprovalQueryService();
        }

        public ActionResult Index(string process = null, int? minAgeDays = null, bool actionableOnly = false)
        {
            var userId = User.GetUserId();
            var auth = BuildAuthorizationService();
            if (!auth.HasPermission(userId, "Assets.View")
                && !auth.HasPermission(userId, "Purchases.View")
                && !auth.HasPermission(userId, "Assets.Request.Approve"))
            {
                return new HttpStatusCodeResult(403, "You do not have permission to access this action.");
            }

            var inbox = _pendingApprovalQuery.BuildInbox(new PendingApprovalUserContextVm
            {
                UserId = userId,
                RoleId = GetCurrentUserRoleId(),
                IsSuperAdmin = IsCurrentUserSuperAdmin(),
                CanApproveAssetRequests = auth.HasPermission(userId, "Assets.Request.Approve")
            });

            var ordered = inbox.Items ?? new List<PendingApprovalQueryItemVm>();
            IEnumerable<PendingApprovalQueryItemVm> filtered = ordered;
            if (!string.IsNullOrWhiteSpace(process))
            {
                filtered = filtered.Where(x => string.Equals(x.ProcessName, process.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (minAgeDays.HasValue && minAgeDays.Value > 0)
            {
                filtered = filtered.Where(x => x.AgeDays >= minAgeDays.Value);
            }

            if (actionableOnly)
            {
                filtered = filtered.Where(x => x.CanCurrentUserAct);
            }

            var filteredList = filtered.Select(MapItem).ToList();
            var model = new PendingApprovalsIndexViewModel
            {
                Items = filteredList,
                TotalPendingCount = inbox.TotalCount,
                ActionableCount = inbox.ActionableCount,
                RequestedByMeCount = inbox.RequestedByMeCount,
                ProcessFilter = process,
                MinAgeDays = minAgeDays,
                ActionableOnly = actionableOnly
            };

            ViewBag.ProcessOptions = ordered.Select(x => x.ProcessName).Distinct().OrderBy(x => x).ToList();
            return View(model);
        }

        private PendingApprovalItemViewModel MapItem(PendingApprovalQueryItemVm item)
        {
            return new PendingApprovalItemViewModel
            {
                ProcessName = item.ProcessName,
                RequestId = item.RequestId,
                AssetId = item.AssetId,
                AssetTag = item.AssetTag,
                AssetName = item.AssetName,
                RequestedById = item.RequestedById,
                RequestedDateText = item.RequestedDateUtc.ToString("yyyy-MM-dd HH:mm"),
                RequestedDateUtc = item.RequestedDateUtc,
                StageNumber = item.StageNumber,
                StageRoleName = item.StageRoleName,
                CanCurrentUserAct = item.CanCurrentUserAct,
                RequestedByCurrentUser = item.RequestedByCurrentUser,
                Summary = item.Summary,
                DetailsUrl = ResolveDetailsUrl(item),
                AgeDays = item.AgeDays,
                AgingBand = item.AgingBand,
                AgingBadgeClass = ResolveAgingBadgeClass(item.AgeDays)
            };
        }

        private string ResolveDetailsUrl(PendingApprovalQueryItemVm item)
        {
            if (string.Equals(item.ProcessName, "Asset Request", StringComparison.OrdinalIgnoreCase))
            {
                return Url.Action("Details", "AssetRequests", new { id = item.RequestId });
            }

            if (string.Equals(item.ProcessName, "Purchase Request", StringComparison.OrdinalIgnoreCase))
            {
                return Url.Action("Details", "PurchaseRequests", new { id = item.RequestId });
            }

            if (item.AssetId > 0)
            {
                return Url.Action("Details", "Assets", new { id = item.AssetId });
            }

            return null;
        }

        private static string ResolveAgingBadgeClass(int ageDays)
        {
            if (ageDays >= 14)
            {
                return "bg-danger";
            }

            if (ageDays >= 7)
            {
                return "bg-warning text-dark";
            }

            return "bg-success";
        }
    }
}
