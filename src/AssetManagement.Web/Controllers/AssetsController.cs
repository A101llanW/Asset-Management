using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    [ModuleAccess(ModulePermissionCatalog.Assets)]
    public class AssetsController : BaseController
    {
        private readonly IAssetService _assetService;
        private readonly IAssetBulkService _assetBulkService;
        private readonly IAssetImportService _assetImportService;
        private readonly IAuthorizationService _authorizationService;

        public AssetsController()
        {
            _assetService = BuildAssetService();
            _assetBulkService = DependencyResolver.Current.GetService<IAssetBulkService>();
            _assetImportService = DependencyResolver.Current.GetService<IAssetImportService>();
            _authorizationService = BuildAuthorizationService();
        }

        public ActionResult Index(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10, string view = "grouped", string groupBy = "product")
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            var isRoomView = AssetRoomGroupView.IsRoomView(view);
            filter.ListViewMode = isRoomView
                ? AssetRoomGroupView.ViewMode
                : string.Equals(view, "grouped", StringComparison.OrdinalIgnoreCase) ? "grouped" : "flat";
            filter.GroupBy = AssetListGroupBy.ResolveFromRequest(filter, groupBy);
            ViewBag.ListViewMode = filter.ListViewMode;
            ViewBag.GroupBy = filter.GroupBy;
            ViewBag.Departments = BuildAssetFilterDepartmentSelectList(filter?.DepartmentId);
            ViewBag.Statuses = new SelectList(System.Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>().Select(x => new { Value = x, Text = x.ToString() }), "Value", "Text", filter?.Status);
            ViewBag.CanBulkEdit = HtmlHasPermission("Assets.Edit");
            ViewBag.CanRelocateAsset = HtmlHasPermission("Assets.Edit") || HtmlHasPermission("Assets.Transfer");
            ViewBag.ClassDepartments = BuildClassDepartmentSelectList();
            ViewBag.RoomDepartmentGroups = BuildMoveTargetDepartmentGroups(DepartmentKind.Room);
            ViewBag.MoveDepartmentGroups = BuildMoveTargetDepartmentGroups();
            ViewBag.Filter = filter;
            SetListSortViewBag(sort, direction);

            if (isRoomView)
            {
                // Group by room: level 1 (one row per unit, rooms first) is a grant-free aggregate;
                // categories and assets load lazily via RoomCategories / RoomCategoryAssets.
                var roomPage = BuildRoomGroupQueryService().GetRoomPage(filter, page, pageSize);
                ViewBag.RoomGroupPage = roomPage;
                return View(ToAssetListPage(new AssetListPageVm
                {
                    Items = new List<AssetListVm>(),
                    TotalCount = roomPage.TotalAssetCount,
                    Search = filter.Search,
                    Sort = sort,
                    Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                    Page = roomPage.Page,
                    PageSize = roomPage.PageSize
                }));
            }

            if (string.Equals(filter.ListViewMode, "grouped", StringComparison.OrdinalIgnoreCase))
            {
                var groupedPage = _assetService.GetAssetGroupListPage(filter, sort, direction, page, pageSize);
                ViewBag.GroupedPage = groupedPage;
                return View(ToAssetListPage(new AssetListPageVm
                {
                    Items = new List<AssetListVm>(),
                    TotalCount = groupedPage.TotalCount,
                    Search = groupedPage.Search,
                    Sort = groupedPage.Sort,
                    Direction = groupedPage.Direction,
                    Page = groupedPage.Page,
                    PageSize = groupedPage.PageSize
                }));
            }

            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            return View(ToAssetListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public JsonResult GroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus,
            int? groupCategoryId,
            int? groupAssetTypeId,
            string groupKey,
            string groupBy = "product",
            bool companyCustodyOnly = false,
            int skip = 0,
            int take = 10)
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            AssetListGroupKeyParts parsedGroupKey;
            if (AssetListGroupKey.TryParse(groupKey, out parsedGroupKey))
            {
                filter.GroupBy = parsedGroupKey.GroupBy;
                groupCategoryId = parsedGroupKey.CategoryId;
                groupAssetTypeId = parsedGroupKey.AssetTypeId;
                assetSubTypeId = parsedGroupKey.AssetSubTypeId;
                groupDepartmentId = parsedGroupKey.DepartmentId;
                groupStatus = parsedGroupKey.Status;
            }
            else
            {
                filter.GroupBy = AssetListGroupBy.ResolveFromRequest(filter, groupBy);
            }

            if (AssetListGroupBy.IsProduct(filter.GroupBy) && string.IsNullOrWhiteSpace(assetName))
            {
                return Json(new { items = new object[0], totalCount = 0, skip = 0, take = take, hasMore = false, remainingCount = 0 }, JsonRequestBehavior.AllowGet);
            }

            var canRelocateAsset = HtmlHasPermission("Assets.Edit") || HtmlHasPermission("Assets.Transfer");
            var pageModel = _assetService.GetAssetGroupMembers(
                filter,
                assetName,
                assetSubTypeId,
                groupDepartmentId,
                groupStatus,
                groupCategoryId,
                groupAssetTypeId,
                skip,
                take);
            EnrichAssetListCustodianNames(pageModel.Items);

            var items = pageModel.Items.Select(x => new
            {
                id = x.Id,
                assetTag = x.AssetTag,
                assetName = x.AssetName,
                brandModel = DisplayText.FormatBrandModel(x.Brand, x.Model),
                categoryName = x.CategoryName,
                assetTypeName = x.AssetTypeName,
                assetSubTypeName = string.IsNullOrWhiteSpace(x.AssetSubTypeName) ? DisplayText.Empty : x.AssetSubTypeName,
                departmentName = string.IsNullOrWhiteSpace(x.DepartmentName) ? "Company custody" : x.DepartmentName,
                statusLabel = x.CurrentStatus.ToString()
                    .Replace("AwaitingApproval", "Pending Approval")
                    .Replace("InStore", "In Store"),
                statusBadgeClass = StatusHtmlHelpers.ToBadgeClass(x.CurrentStatus),
                custodianName = DisplayText.FormatCustodian(x.CurrentCustodianName, x.CurrentStatus),
                acquisitionCost = x.AcquisitionCost,
                acquisitionCostDisplay = AssetPriceRules.Format(x.AcquisitionCost),
                detailsUrl = Url.Action("Details", new { id = x.Id }),
                canMove = canRelocateAsset
                    && string.IsNullOrWhiteSpace(x.CurrentCustodianId)
                    && (x.CurrentStatus == AssetStatus.InStore
                        || x.CurrentStatus == AssetStatus.Received
                        || x.CurrentStatus == AssetStatus.Returned)
            }).ToList();

            var loadedCount = pageModel.Skip + pageModel.Items.Count;
            return Json(new
            {
                items = items,
                totalCount = pageModel.TotalCount,
                skip = pageModel.Skip,
                take = pageModel.Take,
                hasMore = loadedCount < pageModel.TotalCount,
                remainingCount = Math.Max(0, pageModel.TotalCount - loadedCount)
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Group by room, level 2: categories of the filtered assets in one unit (roomId 0 = no unit).</summary>
        [PermissionAuthorize("Assets.View")]
        public JsonResult RoomCategories(AssetFilterVm filter, int roomId = 0)
        {
            filter = ApplyRoomGroupListDefaults(filter);
            var categories = BuildRoomGroupQueryService().GetRoomCategories(filter, roomId);
            var items = categories.Select(x => new
            {
                categoryId = x.CategoryId,
                name = x.Name,
                count = x.AssetCount,
                pricedCount = x.PricedCount,
                unpricedCount = x.UnpricedCount,
                pricedTotal = x.PricedTotal,
                pricedTotalDisplay = x.PricedCount > 0 ? CurrencyFormatter.Format(x.PricedTotal) : AssetPriceRules.NotSetLabel
            }).ToList();

            return Json(new
            {
                items = items,
                totalCount = categories.Sum(x => x.AssetCount)
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Group by room, level 3: the filtered assets in one unit + category, by tag, paged.</summary>
        [PermissionAuthorize("Assets.View")]
        public JsonResult RoomCategoryAssets(AssetFilterVm filter, int roomId = 0, int roomCategoryId = 0, int skip = 0, int take = AssetRoomGroupView.DefaultAssetPageSize)
        {
            filter = ApplyRoomGroupListDefaults(filter);
            var pageModel = BuildRoomGroupQueryService().GetRoomCategoryAssets(filter, roomId, roomCategoryId, skip, take);

            // Reuse the list's custodian label lookup (one batched user query for the page).
            var custodianRows = pageModel.Items
                .Select(x => new AssetListVm { Id = x.Id, CurrentCustodianId = x.CurrentCustodianId, CurrentStatus = x.CurrentStatus })
                .ToList();
            EnrichAssetListCustodianNames(custodianRows);
            var custodianNames = custodianRows.ToDictionary(x => x.Id, x => x.CurrentCustodianName);

            var canEdit = HtmlHasPermission("Assets.Edit");
            var items = pageModel.Items.Select(x =>
            {
                string custodianName;
                custodianNames.TryGetValue(x.Id, out custodianName);
                return new
                {
                    id = x.Id,
                    assetTag = x.AssetTag,
                    assetName = x.AssetName,
                    assetSubTypeName = string.IsNullOrWhiteSpace(x.AssetSubTypeName) ? DisplayText.Empty : x.AssetSubTypeName,
                    custodianName = DisplayText.FormatCustodian(custodianName, x.CurrentStatus),
                    custodianIsPerson = !string.IsNullOrWhiteSpace(custodianName),
                    statusLabel = x.CurrentStatus.ToString()
                        .Replace("AwaitingApproval", "Pending Approval")
                        .Replace("InStore", "In Store"),
                    statusBadgeClass = StatusHtmlHelpers.ToBadgeClass(x.CurrentStatus),
                    price = x.Price,
                    // The asset's own price only: blank = "Not set", never the sub-type price.
                    priceDisplay = x.Price.HasValue ? CurrencyFormatter.Format(x.Price.Value) : AssetPriceRules.NotSetLabel,
                    detailsUrl = Url.Action("Details", new { id = x.Id }),
                    editUrl = canEdit ? Url.Action("Edit", new { id = x.Id }) : null
                };
            }).ToList();

            var loadedCount = pageModel.Skip + pageModel.Items.Count;
            return Json(new
            {
                items = items,
                totalCount = pageModel.TotalCount,
                skip = pageModel.Skip,
                take = pageModel.Take,
                hasMore = loadedCount < pageModel.TotalCount
            }, JsonRequestBehavior.AllowGet);
        }

        private AssetManagement.Application.Contracts.Queries.IAssetRoomGroupQueryService BuildRoomGroupQueryService()
        {
            return DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Queries.IAssetRoomGroupQueryService>();
        }

        /// <summary>Same role defaults as Index so the lazy levels see exactly the Index's asset set.</summary>
        private AssetFilterVm ApplyRoomGroupListDefaults(AssetFilterVm filter)
        {
            filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                filter,
                GetCurrentUserProfile(),
                CanApproveAssetRequests(),
                IsCurrentUserSuperAdmin());
            return filter ?? new AssetFilterVm();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Bulk(AssetBulkActionRequestVm request)
        {
            if (request == null)
            {
                TempData["Error"] = "Bulk action request is required.";
                return RedirectToAction("Index");
            }

            request.PermissionCodes = BuildBulkPermissionCodes();
            try
            {
                var result = _assetBulkService.Execute(request, User.GetUserId());
                TempData["Message"] = "Bulk action completed: " + result.ProcessedCount + " updated, " + result.SkippedCount + " skipped.";
                if (result.Messages != null && result.Messages.Count > 0)
                {
                    TempData["Guidance"] = string.Join(" ", result.Messages.Take(5));
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateToClass(
            int assetId,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped")
        {
            try
            {
                _assetService.RelocateToClassDepartment(assetId, targetDepartmentId, User.GetUserId());
                TempData["Message"] = "Asset moved to the selected class.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            filter = filter ?? new AssetFilterVm();
            return RedirectToAction("Index", new
            {
                Search = filter.Search,
                DepartmentId = filter.DepartmentId,
                Status = filter.Status,
                sort,
                direction,
                page,
                pageSize,
                view
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateGroupToClass(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus groupStatus,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped")
        {
            try
            {
                var result = _assetService.RelocateGroupToClassDepartment(
                    assetName,
                    assetSubTypeId,
                    groupDepartmentId,
                    groupStatus,
                    targetDepartmentId,
                    User.GetUserId());
                TempData["Message"] = "Group move completed: " + result.ProcessedCount + " moved, " + result.SkippedCount + " skipped.";
                if (result.Messages != null && result.Messages.Count > 0)
                {
                    TempData["Guidance"] = string.Join(" ", result.Messages.Take(5));
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            filter = filter ?? new AssetFilterVm();
            return RedirectToAction("Index", new
            {
                Search = filter.Search,
                DepartmentId = filter.DepartmentId,
                Status = filter.Status,
                sort,
                direction,
                page,
                pageSize,
                view
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateToRoom(
            int assetId,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped",
            string groupBy = null)
        {
            try
            {
                _assetService.RelocateToRoomDepartment(assetId, targetDepartmentId, User.GetUserId());
                TempData["Message"] = "Asset moved to the selected room.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToRelocateList(filter, sort, direction, page, pageSize, view, groupBy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AnyPermissionAuthorize("Assets.Edit", "Assets.Transfer")]
        public ActionResult RelocateGroupToRoom(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus groupStatus,
            int targetDepartmentId,
            AssetFilterVm filter,
            string sort = "tag",
            string direction = "asc",
            int page = 1,
            int pageSize = 10,
            string view = "grouped",
            string groupBy = null)
        {
            try
            {
                var result = _assetService.RelocateGroupToRoomDepartment(
                    assetName,
                    assetSubTypeId,
                    groupDepartmentId,
                    groupStatus,
                    targetDepartmentId,
                    User.GetUserId());
                TempData["Message"] = "Group move completed: " + result.ProcessedCount + " moved, " + result.SkippedCount + " skipped.";
                if (result.Messages != null && result.Messages.Count > 0)
                {
                    TempData["Guidance"] = string.Join(" ", result.Messages.Take(5));
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToRelocateList(filter, sort, direction, page, pageSize, view, groupBy);
        }

        private ActionResult RedirectToRelocateList(AssetFilterVm filter, string sort, string direction, int page, int pageSize, string view, string groupBy)
        {
            filter = filter ?? new AssetFilterVm();
            return RedirectToAction("Index", new
            {
                Search = filter.Search,
                DepartmentId = filter.DepartmentId,
                Status = filter.Status,
                sort,
                direction,
                page,
                pageSize,
                view,
                groupBy
            });
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult MyAssets(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            filter.CustodianUserId = User.GetUserId();
            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            ViewBag.ShowCustodianSelfService = true;
            ViewBag.ListTitle = "My Assets";
            ViewBag.ListSubtitle = "Assets currently assigned to you.";
            SetListSortViewBag(sort, direction);
            return View("CustodyList", ToAssetListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult DepartmentAssets(AssetFilterVm filter, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            var user = BuildUserService().GetById(User.GetUserId());
            if (user == null || !user.DepartmentId.HasValue)
            {
                TempData["Error"] = "Your user profile has no department. Contact an administrator.";
                return RedirectToAction("Index");
            }

            filter.DepartmentId = user.DepartmentId;
            var pageModel = _assetService.GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            ViewBag.ListTitle = "Department Assets";
            ViewBag.ListSubtitle = "Assets registered to your department.";
            SetListSortViewBag(sort, direction);
            return View("CustodyList", ToAssetListPage(pageModel));
        }

        /// <summary>
        /// Safe "Back to Assets" target: a local path (FormatSafeReturnPath: no scheme/host, virtual-path aware)
        /// that stays under the current tenant's /Assets pages; anything else (other tenant, other area, external)
        /// falls back to this tenant's Assets/Index.
        /// </summary>
        private string ResolveAssetsListReturnUrl(string returnUrl)
        {
            var fallback = Url.Action("Index");
            var safe = FormatSafeReturnPath(returnUrl);
            if (string.IsNullOrWhiteSpace(safe) || string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            var listBase = fallback.Split('?')[0].TrimEnd('/');
            if (listBase.EndsWith("/Index", StringComparison.OrdinalIgnoreCase))
            {
                listBase = listBase.Substring(0, listBase.Length - "/Index".Length);
            }

            var path = safe.Split('?')[0].TrimEnd('/');
            return string.Equals(path, listBase, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(listBase + "/", StringComparison.OrdinalIgnoreCase)
                ? safe
                : fallback;
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult Details(string id, string returnUrl = null)
        {
            // "Back to Assets" returns to the list the user came from (view, filters, page); only a safe local path
            // under this tenant's Assets pages is accepted, otherwise Assets/Index.
            ViewBag.AssetsBackUrl = ResolveAssetsListReturnUrl(returnUrl);
            int assetId;
            if (!TryResolveAssetId(id, out assetId))
            {
                return HttpNotFound();
            }

            var model = _assetService.GetById(assetId);
            if (model == null)
            {
                return HttpNotFound();
            }

            var currentRoleId = GetCurrentUserRoleId();
            var isSuperAdmin = IsCurrentUserSuperAdmin();
            var currentUserId = User.GetUserId();
            foreach (var pendingTransfer in model.PendingTransfers)
            {
                pendingTransfer.CanCurrentUserApprove = ApprovalWorkflowHelper.CanUserActOnStage(
                    pendingTransfer.RequestedByName,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    pendingTransfer.CurrentStageRoleId,
                    pendingTransfer.CurrentStageUserId);
            }

            if (model.PendingDisposal != null)
            {
                model.PendingDisposal.CanCurrentUserApprove = ApprovalWorkflowHelper.CanUserActOnStage(
                    model.PendingDisposal.RequestedByName,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    model.PendingDisposal.CurrentStageRoleId,
                    model.PendingDisposal.CurrentStageUserId);
            }

            var entity = UnitOfWork.Repository<Asset>().GetById(assetId);
            ViewBag.TransferApprovalSummary = BuildAssetApprovalProcessSummary(entity, ApprovalProcessCodes.Transfer);
            ViewBag.DisposalApprovalSummary = BuildAssetApprovalProcessSummary(entity, ApprovalProcessCodes.Disposal);
            ViewBag.AssetLabelPrint = AssetLabelPrintHelper.CreateModel(Request, Url, model, GetExternalBaseUrl());
            ViewBag.AssetId = assetId;
            try
            {
                ViewBag.AssetAuditLogs = BuildAuditLogService()
                    .GetLogs(new AuditLogFilterVm { RelatedAssetId = assetId, BusinessEventsOnly = true })
                    .OrderByDescending(x => x.Timestamp)
                    .Take(30)
                    .ToList();
            }
            catch (Exception ex)
            {
                // The audit tab is secondary: a slow or failed history query must never take
                // the whole Details page down. Render without it and say so.
                System.Diagnostics.Trace.TraceWarning(
                    "Assets/Details {0}: audit history omitted ({1}: {2})",
                    assetId,
                    ex.GetType().Name,
                    ex.Message);
                ViewBag.AssetAuditLogs = new List<AuditLogVm>();
                ViewBag.AssetAuditLogsUnavailable =
                    "Audit history is temporarily unavailable. Reload the page in a moment to try again.";
            }
            try
            {
                ViewBag.AssetMovements = DependencyResolver.Current.GetService<IAssetMovementService>().GetForAsset(assetId, 100);
            }
            catch (Exception ex)
            {
                // Like the audit tab: the movement history must never take the Details page down.
                System.Diagnostics.Trace.TraceWarning(
                    "Assets/Details {0}: movement history omitted ({1}: {2})",
                    assetId,
                    ex.GetType().Name,
                    ex.Message);
                ViewBag.AssetMovementsUnavailable =
                    "Movement history is temporarily unavailable. Reload the page in a moment to try again.";
            }
            ViewBag.DisposalBlockedReason = BuildDisposalBlockedReason(model);
            EnrichAssetDetails(model);
            model.Documents = BuildAssetDocumentService().GetByAsset(assetId).ToList();
            model.DocumentRows = BuildAssetDocumentRequirementService().GetStatusRowsByAsset(assetId).ToList();
            ViewBag.PendingDocumentRequirementId = Request.QueryString["uploadRequirement"];

            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult PrintLabel(string id)
        {
            int assetId;
            if (!TryResolveAssetId(id, out assetId))
            {
                return HttpNotFound();
            }

            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, assetId, GetExternalBaseUrl());
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult LabelZpl(string id, string codeType = null)
        {
            int assetId;
            if (!TryResolveAssetId(id, out assetId))
            {
                return HttpNotFound();
            }

            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, assetId, GetExternalBaseUrl());
            if (model == null)
            {
                return HttpNotFound();
            }

            var settings = GetLabelPrinterSettings();
            var zpl = ZplLabelBuilder.Build(ToZplLabelData(model), settings, codeType);
            return Content(zpl, "text/plain");
        }

        [PermissionAuthorize("Assets.View")]
        public JsonResult LabelPrintConfig(string id)
        {
            int assetId;
            if (!TryResolveAssetId(id, out assetId))
            {
                return Json(new { error = "Asset not found." }, JsonRequestBehavior.AllowGet);
            }

            var model = AssetLabelPrintHelper.CreateModel(_assetService, Request, Url, assetId, GetExternalBaseUrl());
            if (model == null)
            {
                return Json(new { error = "Asset not found." }, JsonRequestBehavior.AllowGet);
            }

            var settings = GetLabelPrinterSettings();
            var layoutDesign = LabelPrinterSettingsHelper.GetLayoutDesign(settings);
            return Json(new
            {
                enabled = settings.Enabled,
                mode = settings.Mode,
                deviceName = settings.DeviceName,
                zplUrl = Url.Action("LabelZpl", new { id = model.AssetId }),
                labelWidthMm = settings.WidthMm,
                labelHeightMm = settings.HeightMm,
                qrMagnification = settings.QrMagnification,
                usesCustomLayout = LabelPrinterSettingsHelper.UsesCustomLayout(settings),
                layoutDesign = layoutDesign,
                assetId = model.AssetId,
                defaultCodeType = LabelPrinterSettingsHelper.CodeTypeQr,
                codeTypes = new[]
                {
                    new { value = LabelPrinterSettingsHelper.CodeTypeQr, text = "QR code" },
                    new { value = LabelPrinterSettingsHelper.CodeTypeBarcode, text = "Barcode" }
                }
            }, JsonRequestBehavior.AllowGet);
        }

        private string GetExternalBaseUrl()
        {
            var platformSettings = DependencyResolver.Current.GetService<IPlatformSettingsService>();
            return platformSettings == null ? null : platformSettings.GetExternalBaseUrl();
        }

        private LabelPrinterSettingsVm GetLabelPrinterSettings()
        {
            return LabelPrinterSettingsHelper.FromDictionary(
                ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll()));
        }

        private static ZplLabelData ToZplLabelData(AssetManagement.Web.ViewModels.AssetLabelPrintVm model)
        {
            return new ZplLabelData
            {
                AssetTag = model.AssetTag,
                AssetName = model.AssetName,
                DepartmentName = model.DepartmentName,
                SerialNumber = model.SerialNumber,
                ScanUrl = model.ScanUrl,
                BarcodePayload = model.BarcodePayload
            };
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create()
        {
            var model = new AssetCreateVm
            {
                CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore,
                PurchaseDate = KenyaTimeHelper.FromUtc(System.DateTime.UtcNow).Date,
                DepreciationStartDate = KenyaTimeHelper.FromUtc(System.DateTime.UtcNow).Date,
                ApprovalProcesses = AssetApprovalSettingsHelper.BuildDefaultProcesses(UnitOfWork, GetRolesForOrganization(), ResolveCurrentOrganizationId()).ToList()
            };

            ApplyAssetFormDefaults(model);
            PopulateLookups(model);
            PopulateDepreciationContext(model);
            PopulateAssetApprovalFormOptions();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public ActionResult Create([Bind(Prefix = "")] AssetCreateVm viewModel)
        {
            if (viewModel == null)
            {
                viewModel = new AssetCreateVm
                {
                    CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore
                };
            }

            if (viewModel.CurrentStatus == 0)
            {
                viewModel.CurrentStatus = AssetManagement.Domain.Enums.AssetStatus.InStore;
            }

            AssetTaxInputHelper.ApplyTaxInput(viewModel);
            ApplyAssetFormDefaults(viewModel);
            ClearOptionalAssetFieldErrors(viewModel);
            PopulateLookups(viewModel);
            PopulateDepreciationContext(viewModel);
            PopulateAssetApprovalFormOptions();
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                viewModel.CanManageDepreciationSettings = CanManageDepreciationSettings();
                var assetId = _assetService.Create(viewModel);
                var guidance = AppendAcquisitionCostApplyResult(assetId, viewModel);
                if (string.IsNullOrWhiteSpace(guidance))
                {
                    guidance = "Next step: review the asset details, then assign it, transfer it, or add maintenance and insurance information.";
                }

                TempData["Message"] = "Asset created successfully.";
                TempData["Guidance"] = guidance;
                return RedirectToAction("Details", new { id = assetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateAssetApprovalFormOptions();
                return View(viewModel);
            }
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Create")]
        public ActionResult Import(System.Web.HttpPostedFileBase importFile)
        {
            if (importFile == null || importFile.ContentLength == 0)
            {
                TempData["Error"] = "Select an Excel or CSV file to import.";
                return RedirectToAction("Import");
            }

            try
            {
                using (var stream = importFile.InputStream)
                {
                    var result = _assetImportService.Import(stream, importFile.FileName, User.GetUserId());
                    TempData["Message"] = "Import completed: " + result.ImportedCount + " assets created, " + result.SkippedCount + " skipped.";
                    if (result.Messages != null && result.Messages.Count > 0)
                    {
                        TempData["Guidance"] = string.Join(" ", result.Messages.Take(10));
                    }
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Import");
            }

            return RedirectToAction("Index");
        }

        [PermissionAuthorize("Assets.Create")]
        public ActionResult DownloadImportTemplate()
        {
            return File(
                _assetImportService.GetImportTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "asset-import-template.xlsx");
        }

        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit(int id)
        {
            // The form needs only the asset row. _assetService.GetById builds the whole Details model
            // (custody timeline, pending transfers/disposals, maintenance, incidents, claims, policies,
            // cost of ownership): ~10 extra queries that Edit never shows.
            var entity = UnitOfWork.Repository<Asset>().GetById(id);
            // Soft-deleted assets cannot be saved (AssetService.Update EnsureActive), so do not open the form.
            if (entity == null || !entity.IsActive || !CanOpenAssetForEdit(entity))
            {
                return HttpNotFound();
            }

            var model = new AssetEditVm
            {
                Id = entity.Id,
                AssetName = entity.AssetName,
                AssetTag = entity.AssetTag,
                SerialNumber = entity.SerialNumber,
                Brand = entity.Brand,
                Model = entity.Model,
                CategoryId = entity.CategoryId,
                AssetTypeId = entity.AssetTypeId,
                AssetSubTypeId = entity.AssetSubTypeId,
                AssetSubTypeName = entity.AssetSubTypeId.HasValue
                    ? ResolveAssetSubTypeName(entity.AssetSubTypeId.Value)
                    : null,
                DepartmentId = entity.DepartmentId,
                SupplierId = entity.SupplierId,
                PurchaseDate = entity.PurchaseDate,
                AcquisitionCost = entity.AcquisitionCost,
                PreviousAcquisitionCost = entity.AcquisitionCost,
                Currency = entity.Currency,
                CurrentStatus = entity.CurrentStatus,
                UsefulLifeMonths = entity.UsefulLifeMonths,
                SalvageValue = entity.SalvageValue,
                TaxAmount = entity.TaxAmount,
                ConditionOnReceipt = entity.ConditionOnReceipt,
                DepreciationMethod = entity.DepreciationMethod,
                DepreciationStartDate = entity.DepreciationStartDate,
                UseCustomDepreciationLife = entity.DepreciationLifeMonths.HasValue,
                DepreciationLifeMonths = entity.DepreciationLifeMonths,
                UseCustomDepreciationRate = entity.DepreciationRatePercent.HasValue,
                DepreciationRatePercent = entity.DepreciationRatePercent,
                IsInsured = entity.IsInsured,
                InsuredValue = entity.InsuredValue,
                WarrantyStartDate = entity.WarrantyStartDate,
                WarrantyEndDate = entity.WarrantyEndDate,
                Description = entity.Description,
                ApprovalProcesses = AssetApprovalSettingsHelper.BuildFromAsset(
                    entity,
                    UnitOfWork,
                    GetRolesForOrganization(entity.OrganizationId),
                    entity.OrganizationId.HasValue
                        ? ApproverPickerHelper.BuildUserNameLookup(
                            BuildReferenceDataCache().GetUsersForDropdown(entity.OrganizationId.Value))
                        : null).ToList()
            };

            AssetTaxInputHelper.SeedTaxInputFromStoredAmount(model);
            ApplyAssetFormDefaults(model);
            PopulateLookups(model);
            PopulateDepreciationContext(model, entity);
            PopulateAssetApprovalFormOptions(entity.OrganizationId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Edit([Bind(Prefix = "")] AssetEditVm viewModel)
        {
            var assetEntity = UnitOfWork.Repository<Asset>().GetById(viewModel.Id);
            AssetTaxInputHelper.ApplyTaxInput(viewModel);
            ApplyAssetFormDefaults(viewModel);
            ClearOptionalAssetFieldErrors(viewModel);
            PopulateLookups(viewModel);
            PopulateDepreciationContext(viewModel, assetEntity);
            PopulateAssetApprovalFormOptions(assetEntity == null ? null : assetEntity.OrganizationId);
            AssetApprovalSettingsHelper.ValidateApprovalProcesses(viewModel.ApprovalProcesses, (key, message) => ModelState.AddModelError(key, message));
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                viewModel.CanManageDepreciationSettings = CanManageDepreciationSettings();
                _assetService.Update(viewModel);
                var message = "Asset updated successfully.";
                var guidance = AppendAcquisitionCostApplyResult(viewModel.Id, viewModel);
                if (!string.IsNullOrWhiteSpace(guidance))
                {
                    TempData["Guidance"] = guidance;
                }

                TempData["Message"] = message;
                return RedirectToAction("Details", new { id = viewModel.Id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                PopulateAssetApprovalFormOptions(assetEntity == null ? null : assetEntity.OrganizationId);
                return View(viewModel);
            }
        }

        [HttpGet]
        public JsonResult AcquisitionCostApplyPreview(
            int id,
            string scope,
            int? categoryId,
            int? assetTypeId,
            int? assetSubTypeId,
            int? departmentId,
            AssetStatus? status)
        {
            try
            {
                var filter = BuildPriceApplyFilter(categoryId, assetTypeId, assetSubTypeId, departmentId, status);
                var count = _assetService.CountAcquisitionCostApplyCandidates(id, scope, filter);
                return Json(new { success = true, count = count }, JsonRequestBehavior.AllowGet);
            }
            catch (BusinessException ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Delete")]
        public ActionResult Delete(int id)
        {
            _assetService.Delete(id);
            TempData["Message"] = "Asset archived.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Dispose")]
        public ActionResult RequestDisposal(int id, string disposalReason, DisposalMethod disposalMethod, string notes)
        {
            try
            {
                _assetService.RequestDisposal(new AssetDisposalRequestVm
                {
                    AssetId = id,
                    DisposalReason = disposalReason,
                    DisposalMethod = disposalMethod,
                    Notes = notes
                }, User.GetUserId());
                TempData["Message"] = "Disposal request submitted.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.ApproveDisposal")]
        public ActionResult ApproveDisposal(int id, decimal? disposalAmount, string notes)
        {
            try
            {
                _assetService.ApproveDisposal(new AssetDisposalApprovalVm
                {
                    AssetId = id,
                    DisposalAmount = disposalAmount,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                var asset = _assetService.GetById(id);
                TempData["Message"] = asset != null && asset.CurrentStatus == AssetStatus.Disposed
                    ? "Disposal approved and asset marked as disposed."
                    : "Disposal stage approved. Additional approval may still be required.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.ApproveDisposal")]
        public ActionResult RejectDisposal(int id, string notes)
        {
            try
            {
                _assetService.RejectDisposal(new AssetDisposalApprovalVm
                {
                    AssetId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Disposal request rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetailsTab(id, "disposal");
        }

        private string BuildDisposalBlockedReason(AssetDetailsVm model)
        {
            if (model == null)
            {
                return null;
            }

            if (model.CurrentStatus == AssetStatus.Disposed || model.CurrentStatus == AssetStatus.Retired)
            {
                return "This asset is already disposed or retired.";
            }

            if (model.PendingDisposal != null)
            {
                return null;
            }

            if (model.CurrentStatus == AssetStatus.AwaitingApproval)
            {
                return "Another approval workflow is in progress for this asset (for example a transfer). Resolve it before requesting disposal.";
            }

            return null;
        }

        private ActionResult RedirectToAssetDetailsTab(int id, string tab)
        {
            var url = Url.Action("Details", new { id }) + "#" + tab;
            return Redirect(url);
        }

        private bool CanApproveAssetRequests()
        {
            return _authorizationService.HasPermission(User.GetUserId(), "Assets.Request.Approve");
        }

        private IList<string> BuildBulkPermissionCodes()
        {
            var userId = User.GetUserId();
            var codes = new List<string>();
            if (_authorizationService.HasPermission(userId, "Assets.Edit"))
            {
                codes.Add("Assets.Edit");
            }

            if (_authorizationService.HasPermission(userId, "Assets.Assign"))
            {
                codes.Add("Assets.Assign");
            }

            return codes;
        }

        private bool TryResolveAssetId(string id, out int assetId)
        {
            assetId = 0;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (int.TryParse(id, out assetId) && assetId > 0)
            {
                return true;
            }

            var lookup = _assetService.LookupByScanCode(id, true, false);
            if (lookup == null || !lookup.Found || !lookup.AssetId.HasValue || lookup.AssetId.Value <= 0)
            {
                return false;
            }

            assetId = lookup.AssetId.Value;
            return true;
        }

        private bool HtmlHasPermission(string code)
        {
            return _authorizationService.HasPermission(User.GetUserId(), code);
        }

        private void PopulateLookups(AssetCreateVm model)
        {
            ViewBag.Categories = BuildCategorySelectList(model?.CategoryId, activeOnly: false);
            ViewBag.AssetTypeOptions = BuildAssetTypeLookupList(activeOnly: false);

            ViewBag.Departments = BuildDepartmentSelectList(model?.DepartmentId, activeOnly: false);
            ViewBag.DepartmentGroups = BuildGroupedDepartmentSelectGroups(model?.DepartmentId, activeOnly: true, includeInactiveDepartmentId: model?.DepartmentId);
            ViewBag.Suppliers = BuildSupplierSelectList(model?.SupplierId, activeOnly: false);
            ViewBag.OrganizationCurrency = GetDefaultCurrencyCode();
            ViewBag.SubTypeLookupUrl = TenantUrlHelper.TenantRouteUrl(Url, "Lookup", "AssetSubTypes");
            ViewBag.SubTypeByTypeUrl = TenantUrlHelper.TenantRouteUrl(Url, "ByType", "AssetSubTypes");
            ViewBag.SubTypeCreateUrl = TenantUrlHelper.TenantRouteUrl(Url, "CreateFromAsset", "AssetSubTypes");
            ViewBag.Statuses = new SelectList(
                System.Enum.GetValues(typeof(AssetStatus)).Cast<AssetStatus>().Select(x => new { Value = x, Text = x.ToString() }),
                "Value",
                "Text",
                model == null ? null : model.PriceApplyStatus);
            ViewBag.PriceApplyPreviewUrl = TenantUrlHelper.TenantRouteUrl(Url, "AcquisitionCostApplyPreview", "Assets");
        }

        /// <summary>
        /// Same visibility as _assetService.GetById (department scope, or an approver of a pending
        /// transfer of this asset) without building the Details model. The fallback only runs when the
        /// department-scope check fails.
        /// </summary>
        private bool CanOpenAssetForEdit(Asset entity)
        {
            var departmentScope = DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.IDepartmentScopeService>();
            if (departmentScope == null)
            {
                return _assetService.GetById(entity.Id) != null;
            }

            try
            {
                departmentScope.EnsureCanAccessAsset(entity);
                return ResolveCurrentOrganizationId().HasValue;
            }
            catch (BusinessException)
            {
                return _assetService.GetById(entity.Id) != null;
            }
        }

        private string ResolveAssetSubTypeName(int subTypeId)
        {
            // Name only: the sub-type service's GetById also computes stock counts, type and category.
            var subType = UnitOfWork.Repository<AssetSubType>().GetById(subTypeId);
            return subType == null ? null : AssetSubTypeNormalizer.NormalizeName(subType.Name);
        }

        private bool CanManageDepreciationSettings()
        {
            var userId = User.GetUserId();
            return _authorizationService.HasPermission(userId, "Depreciation.Manage")
                || _authorizationService.HasPermission(userId, "Financials.Edit");
        }

        private void PopulateDepreciationContext(AssetCreateVm model, Asset entity = null)
        {
            ViewBag.CanManageDepreciation = CanManageDepreciationSettings();
            if (model == null || model.CategoryId <= 0 || model.AssetTypeId <= 0)
            {
                ViewBag.EffectiveDepreciationLifeMonths = null;
                ViewBag.EffectiveDepreciationRatePercent = null;
                ViewBag.EffectiveDepreciationLifeSource = null;
                ViewBag.EffectiveDepreciationRateSource = null;
                return;
            }

            var previewAsset = entity ?? new Asset
            {
                CategoryId = model.CategoryId,
                AssetTypeId = model.AssetTypeId,
                UsefulLifeMonths = UsefulLifeResolver.Resolve(UnitOfWork, model.AssetTypeId, model.CategoryId),
                DepreciationLifeMonths = model.UseCustomDepreciationLife ? model.DepreciationLifeMonths : null,
                DepreciationRatePercent = model.UseCustomDepreciationRate ? model.DepreciationRatePercent : null,
                DepreciationMethod = model.DepreciationMethod,
                AcquisitionCost = model.AcquisitionCost,
                SalvageValue = model.SalvageValue,
                CurrentBookValue = AssetPriceRules.OpeningBookValue(model.AcquisitionCost)
            };

            if (entity != null)
            {
                previewAsset.UsefulLifeMonths = entity.UsefulLifeMonths;
                previewAsset.DepreciationLifeMonths = model.UseCustomDepreciationLife ? model.DepreciationLifeMonths : entity.DepreciationLifeMonths;
                previewAsset.DepreciationRatePercent = model.UseCustomDepreciationRate ? model.DepreciationRatePercent : entity.DepreciationRatePercent;
                previewAsset.CurrentBookValue = entity.CurrentBookValue;
            }

            var assetType = UnitOfWork.Repository<AssetType>().GetById(model.AssetTypeId);
            var category = UnitOfWork.Repository<AssetCategory>().GetById(model.CategoryId);
            var settings = DepreciationSettingsResolver.Resolve(previewAsset, assetType, category);
            ViewBag.EffectiveDepreciationLifeMonths = settings.LifeMonths;
            ViewBag.EffectiveDepreciationRatePercent = settings.AnnualRatePercent;
            ViewBag.EffectiveDepreciationLifeSource = settings.LifeSource;
            ViewBag.EffectiveDepreciationRateSource = settings.RateSource;
        }

        private void ApplyAssetFormDefaults(AssetCreateVm model)
        {
            if (model == null)
            {
                return;
            }

            model.Currency = GetDefaultCurrencyCode();
            if (model.DepartmentId.HasValue && model.DepartmentId.Value <= 0)
            {
                model.DepartmentId = null;
            }

            if (model.SupplierId.HasValue && model.SupplierId.Value <= 0)
            {
                model.SupplierId = null;
            }
        }

        // Removed 2026-10-08 (Allan): ApplyAcquisitionCostFromSubTypeDefault used to copy the sub-type's
        // default price into a blank Acquisition cost on Create/Edit. A blank price now stays "Not set".

        private void ClearOptionalAssetFieldErrors(AssetCreateVm model)
        {
            ModelState.Remove("CurrentStatus");
            ModelState.Remove("AssetTag");
            ModelState.Remove("Currency");
            ModelState.Remove("DepartmentId");
            ModelState.Remove("SupplierId");
            ModelState.Remove("Brand");
            ModelState.Remove("Model");
            ModelState.Remove("AssetSubTypeName");

            if (model == null)
            {
                return;
            }

            ModelState.SetModelValue("Currency", new ValueProviderResult(model.Currency, model.Currency, System.Globalization.CultureInfo.InvariantCulture));
            ModelState.SetModelValue(
                "DepartmentId",
                new ValueProviderResult(
                    model.DepartmentId?.ToString() ?? string.Empty,
                    model.DepartmentId?.ToString() ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture));
            ModelState.SetModelValue(
                "SupplierId",
                new ValueProviderResult(
                    model.SupplierId?.ToString() ?? string.Empty,
                    model.SupplierId?.ToString() ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        private void PopulateRoleOptions()
        {
            ViewBag.RoleOptions = BuildRoleOptionList();
        }

        private void PopulateAssetApprovalFormOptions(int? organizationId = null)
        {
            PopulateRoleOptions();
        }

        private string AppendAcquisitionCostApplyResult(int assetId, AssetCreateVm model)
        {
            if (model == null || !model.AcquisitionCost.HasValue)
            {
                // Nothing to copy: a "Not set" price is never bulk-applied to other assets.
                return null;
            }

            var scope = string.IsNullOrWhiteSpace(model.AcquisitionCostApplyScope)
                ? AcquisitionCostApplyScopes.Individual
                : model.AcquisitionCostApplyScope.Trim();

            if (string.Equals(scope, AcquisitionCostApplyScopes.Individual, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var isEdit = model is AssetEditVm;
            if (isEdit && model.AcquisitionCost == model.PreviousAcquisitionCost)
            {
                return null;
            }

            var filter = BuildPriceApplyFilter(
                model.PriceApplyCategoryId,
                model.PriceApplyAssetTypeId,
                model.PriceApplyAssetSubTypeId ?? model.AssetSubTypeId,
                model.PriceApplyDepartmentId,
                model.PriceApplyStatus);

            var result = _assetService.ApplyAcquisitionCost(
                assetId,
                model.AcquisitionCost.Value,
                scope,
                filter,
                User.GetUserId());

            if (result.ProcessedCount <= 0 && result.SkippedCount <= 0)
            {
                return null;
            }

            var guidance = "Price also applied to " + result.ProcessedCount + " other asset(s).";
            if (result.SkippedCount > 0)
            {
                guidance += " " + result.SkippedCount + " skipped.";
            }

            if (result.Messages != null && result.Messages.Count > 0)
            {
                guidance += " " + string.Join(" ", result.Messages.Take(3));
            }

            return guidance;
        }

        private static AssetFilterVm BuildPriceApplyFilter(
            int? categoryId,
            int? assetTypeId,
            int? assetSubTypeId,
            int? departmentId,
            AssetStatus? status)
        {
            return new AssetFilterVm
            {
                CategoryId = categoryId,
                AssetTypeId = assetTypeId,
                AssetSubTypeId = assetSubTypeId,
                DepartmentId = departmentId,
                Status = status
            };
        }
    }
}

