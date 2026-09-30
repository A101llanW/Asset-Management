using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [AnyPermissionAuthorize("Purchases.View", "Purchases.Create", "Purchases.Approve")]
    public class PurchaseRequestsController : BaseController
    {
        private static readonly string[] AllowedAttachmentExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv" };
        private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly IFileStorageProvider _storage;

        public PurchaseRequestsController()
        {
            _purchaseRequestService = BuildPurchaseRequestService();
            _storage = DependencyResolver.Current.GetService<IFileStorageProvider>();
        }

        public ActionResult Index(string search = null, string sort = "created", string direction = "desc", int page = 1, int pageSize = 10)
        {
            var pageResult = _purchaseRequestService.GetListPage(search, sort, direction, page, pageSize);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(ToListPage(pageResult));
        }

        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(string returnUrl = null, int? fromAssetRequestId = null)
        {
            var model = new PurchaseRequestCreateVm
            {
                Currency = GetDefaultCurrencyCode(),
                Quantity = 1
            };

            PrefillScopedRequisitionTarget(model);

            if (fromAssetRequestId.HasValue)
            {
                var assetRequest = BuildAssetRequestService().GetById(fromAssetRequestId.Value);
                if (assetRequest != null)
                {
                    if (assetRequest.DepartmentId.HasValue)
                    {
                        model.DepartmentId = assetRequest.DepartmentId.Value;
                    }

                    model.ItemDescription = !string.IsNullOrWhiteSpace(assetRequest.RequestedAssetName)
                        ? assetRequest.RequestedAssetName
                        : assetRequest.CategoryName;
                    model.Justification = assetRequest.Justification;
                    if (assetRequest.RequestedAssetId.HasValue)
                    {
                        model.TargetAssetId = assetRequest.RequestedAssetId;
                    }
                }
            }

            PopulateCreateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            PopulatePurchaseApprovalPathPreview(model.DepartmentId > 0 ? (int?)model.DepartmentId : null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(PurchaseRequestCreateVm model, HttpPostedFileBase attachment, string returnUrl = null)
        {
            EnforceScopedRequisitionTarget(model);
            if (!HasPermission("Purchases.CreateForAnyDepartment"))
            {
                model.OrderByUserId = null;
            }

            PopulateCreateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            PopulatePurchaseApprovalPathPreview(model != null && model.DepartmentId > 0 ? (int?)model.DepartmentId : null);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var id = _purchaseRequestService.Submit(model, User.GetUserId());
                if (!HasPermission("Purchases.CreateForAnyDepartment"))
                {
                    SaveOptionalAttachment(id, attachment);
                }
                TempData["Message"] = "Requisition submitted.";
                return RedirectToAction("Details", new { id, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            EnrichUserNames(model);
            var currentRoleId = GetCurrentUserRoleId();
            var isSuperAdmin = IsCurrentUserSuperAdmin();
            var currentUserId = User.GetUserId();
            model.CanCurrentUserApprove = model.IsPending
                && ApprovalWorkflowHelper.CanUserActOnStage(
                    UnitOfWork,
                    model.RequestedById,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    model.CurrentStageRoleId,
                    model.CurrentStageUserId,
                    allowEligibleSelfApproval: true);

            if (model.IsPending && !model.CanCurrentUserApprove)
            {
                model.CannotApproveReason = BuildCannotApproveReason(model, currentUserId, currentRoleId);
            }

            model.ApprovalPathSourceLabel = "Snapshotted stages on this request";
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(model);
        }

        [PermissionAuthorize("Purchases.View")]
        public ActionResult DownloadAttachment(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null || !model.HasAttachment)
            {
                return HttpNotFound();
            }

            var relativePath = _purchaseRequestService.GetAttachmentRelativePath(id);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return HttpNotFound();
            }

            var content = _storage.OpenRead(relativePath);
            if (content == null)
            {
                return HttpNotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(model.AttachmentContentType)
                ? "application/octet-stream"
                : model.AttachmentContentType;
            return File(content, contentType, model.AttachmentFileName);
        }

        [PermissionAuthorize("Purchases.View")]
        public ActionResult DownloadDocument(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            EnrichUserNames(model);
            var generatedBy = User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "System";
            var branding = ReportBrandingHelper.Resolve(
                UnitOfWork,
                DependencyResolver.Current.GetService<IOrganizationScopeService>(),
                ResolveApplicationBaseUrl());
            var html = ReportHtmlBuilder.BuildRequisitionDocument(model, generatedBy, branding);
            var fileName = SanitizeDownloadFileName(model.RequestNumber) + ".html";
            return File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", fileName);
        }

        [PermissionAuthorize("Purchases.View")]
        public JsonResult DocumentFragment(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return Json(new { success = false, message = "Requisition not found." }, JsonRequestBehavior.AllowGet);
            }

            EnrichUserNames(model);
            var generatedBy = User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "System";
            var branding = ReportBrandingHelper.Resolve(
                UnitOfWork,
                DependencyResolver.Current.GetService<IOrganizationScopeService>(),
                ResolveApplicationBaseUrl());
            var html = ReportHtmlBuilder.BuildRequisitionFragment(model, generatedBy, branding);
            var fileName = SanitizeDownloadFileName(model.RequestNumber) + ".pdf";
            return Json(new { success = true, html, fileName }, JsonRequestBehavior.AllowGet);
        }

        private static string SanitizeDownloadFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "requisition";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "requisition" : cleaned;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Approve(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Approve(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Requisition approval recorded.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Reject(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Reject(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Requisition rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        [PermissionAuthorize("Purchases.Create")]
        public JsonResult SearchTargetAssets(string search = null, int? departmentId = null, AssetStatus? status = null, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var filter = new AssetFilterVm
            {
                Search = search,
                DepartmentId = departmentId,
                Status = status,
                OrganizationWide = true
            };

            var pageModel = BuildAssetService().GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            var listPage = ToAssetListPage(pageModel);
            var inStockLookup = BuildInStockQuantityLookup(pageModel.Items);

            var items = pageModel.Items.Select(x => new
            {
                id = x.Id,
                assetTag = x.AssetTag,
                assetName = x.AssetName,
                categoryName = x.CategoryName,
                departmentId = x.DepartmentId,
                departmentName = string.IsNullOrWhiteSpace(x.DepartmentName) ? "Company custody" : x.DepartmentName,
                custodianName = string.IsNullOrWhiteSpace(x.CurrentCustodianName) ? "—" : x.CurrentCustodianName,
                status = FormatAssetStatusLabel(x.CurrentStatus),
                statusBadge = StatusHtmlHelpers.ToBadgeClass(x.CurrentStatus),
                acquisitionCost = x.AcquisitionCost,
                acquisitionCostDisplay = CurrencyFormatter.Format(x.AcquisitionCost),
                label = FormatTargetAssetLabel(x),
                itemDescription = FormatTargetAssetItemDescription(x),
                subTypeName = string.IsNullOrWhiteSpace(x.AssetSubTypeName) ? "—" : x.AssetSubTypeName,
                quantityInStock = ResolveInStockQuantity(x, inStockLookup)
            }).ToList();

            return Json(new
            {
                items = items,
                page = listPage.Page,
                pageSize = listPage.PageSize,
                totalCount = listPage.TotalCount,
                totalPages = listPage.TotalPages,
                startItem = listPage.StartItem,
                endItem = listPage.EndItem,
                sort = sort,
                direction = direction
            }, JsonRequestBehavior.AllowGet);
        }

        [PermissionAuthorize("Purchases.Create")]
        public JsonResult AvailableTargetAssets()
        {
            var items = GetTargetAssetOptions()
                .Select(x => new { id = x.Value, name = x.Text })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        private void PopulateCreateLookups(PurchaseRequestCreateVm model)
        {
            var canCreateForAnyDepartment = HasPermission("Purchases.CreateForAnyDepartment");
            var userDepartmentId = GetCurrentUserDepartmentId();
            int? scopeRoot = null;
            if (!canCreateForAnyDepartment && !IsCurrentUserSuperAdmin())
            {
                scopeRoot = userDepartmentId;
            }

            int? departmentId = model == null ? userDepartmentId : (int?)model.DepartmentId;
            var selectList = BuildRequisitionDepartmentSelectList(departmentId, scopeRoot);
            var targetCount = selectList != null ? selectList.Count() : 0;
            var userDeptIsTarget = userDepartmentId.HasValue
                && selectList != null
                && selectList.Any(x => x.Value == userDepartmentId.Value.ToString());
            var lockDepartment = !canCreateForAnyDepartment
                && !IsCurrentUserSuperAdmin()
                && userDeptIsTarget
                && targetCount <= 1;

            ViewBag.CanCreateForAnyDepartment = canCreateForAnyDepartment;
            ViewBag.LockDepartment = lockDepartment;
            ViewBag.DepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                departmentId,
                GetActiveDepartments());
            ViewBag.Departments = selectList;
            ViewBag.OrderByUsers = BuildOrderByUserSelectList(departmentId, model?.OrderByUserId);
            ViewBag.TargetAssetSearchUrl = TenantUrlHelper.TenantRouteUrl(Url, "SearchTargetAssets", "PurchaseRequests");
            ViewBag.PreviewApprovalPathUrl = TenantUrlHelper.TenantRouteUrl(Url, "PreviewApprovalPath", "PurchaseRequests");
            ViewBag.SelectedTargetAssetLabel = ResolveSelectedTargetAssetLabel(model?.TargetAssetId);
        }

        [HttpGet]
        [PermissionAuthorize("Purchases.Create")]
        public JsonResult PreviewApprovalPath(int departmentId)
        {
            var preview = BuildPurchaseApprovalPathPreview(departmentId);
            if (preview == null)
            {
                return Json(new
                {
                    hasApprovers = false,
                    sourceKind = "OrganizationMatrix",
                    sourceLabel = "Organization Approval Matrix",
                    summary = "Select a requisition target to preview the approval path.",
                    stages = new object[0]
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                hasApprovers = preview.HasApprovers,
                sourceKind = preview.SourceKind,
                sourceLabel = preview.SourceLabel,
                summary = preview.Summary,
                stages = preview.Stages.Select(s => new
                {
                    stageNumber = s.StageNumber,
                    roleId = s.RoleId,
                    roleName = s.RoleName,
                    userId = s.UserId,
                    userName = s.UserName,
                    displayLabel = s.DisplayLabel
                }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        private void PrefillScopedRequisitionTarget(PurchaseRequestCreateVm model)
        {
            if (model == null || IsCurrentUserSuperAdmin() || HasPermission("Purchases.CreateForAnyDepartment"))
            {
                return;
            }

            var userDepartmentId = GetCurrentUserDepartmentId();
            if (!userDepartmentId.HasValue)
            {
                return;
            }

            var options = BuildRequisitionDepartmentSelectList(null, userDepartmentId);
            if (options.Any(x => x.Value == userDepartmentId.Value.ToString()))
            {
                model.DepartmentId = userDepartmentId.Value;
                return;
            }

            var first = options.FirstOrDefault();
            if (first != null && !string.IsNullOrWhiteSpace(first.Value))
            {
                int parsed;
                if (int.TryParse(first.Value, out parsed))
                {
                    model.DepartmentId = parsed;
                }
            }
        }

        private void EnforceScopedRequisitionTarget(PurchaseRequestCreateVm model)
        {
            if (model == null || IsCurrentUserSuperAdmin() || HasPermission("Purchases.CreateForAnyDepartment"))
            {
                return;
            }

            var userDepartmentId = GetCurrentUserDepartmentId();
            if (!userDepartmentId.HasValue)
            {
                return;
            }

            var allowed = BuildRequisitionDepartmentSelectList(null, userDepartmentId)
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (allowed.Count == 1)
            {
                int onlyId;
                if (int.TryParse(allowed[0], out onlyId))
                {
                    model.DepartmentId = onlyId;
                }
                return;
            }

            if (!allowed.Contains(model.DepartmentId.ToString()))
            {
                ModelState.AddModelError("DepartmentId", "Select a requisition target under your department branch (room, leaf sub-unit, leaf admin, or class/stream).");
            }
        }

        private void PopulatePurchaseApprovalPathPreview(int? departmentId)
        {
            var preview = departmentId.HasValue && departmentId.Value > 0
                ? BuildPurchaseApprovalPathPreview(departmentId.Value)
                : null;
            ViewBag.PurchaseApprovalPath = preview;
            ViewBag.PurchaseApprovalSummary = preview == null
                ? "Select a requisition target to see the approval path submit will use."
                : preview.SourceLabel + (string.IsNullOrWhiteSpace(preview.Summary) ? "" : " — " + preview.Summary);
        }

        private ResolvedRequisitionFlowPreviewVm BuildPurchaseApprovalPathPreview(int departmentId)
        {
            var department = UnitOfWork.Repository<Department>().GetById(departmentId);
            if (department == null)
            {
                return null;
            }

            var orgDefault = ApprovalWorkflowHelper.GetProcessConfiguration(UnitOfWork, ApprovalProcessCodes.Purchase);
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(
                department,
                id => UnitOfWork.Repository<Department>().GetById(id),
                orgDefault);
            var config = detailed.Configuration ?? orgDefault;
            var stageRoles = config.StageRoleIds ?? new List<int>();
            var stageUsers = config.StageUserIds ?? new List<string>();
            var roleLookup = BuildRoleNameLookup();
            var orgId = ResolveCurrentOrganizationId();
            var users = orgId.HasValue
                ? BuildReferenceDataCache().GetUsersForDropdown(orgId.Value)
                : GetActiveUsers();
            var userLookup = ApproverPickerHelper.BuildUserNameLookup(users);

            var stages = new List<ResolvedRequisitionFlowStageVm>();
            for (var i = 0; i < stageRoles.Count; i++)
            {
                var roleId = stageRoles[i];
                var roleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, roleId);
                var userId = i < stageUsers.Count ? stageUsers[i] : null;
                var userName = null as string;
                if (!string.IsNullOrWhiteSpace(userId) && userLookup != null && userLookup.ContainsKey(userId))
                {
                    userName = userLookup[userId];
                }

                var label = roleName;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    label = roleName + " (" + userName + ")";
                }
                else if (!string.IsNullOrWhiteSpace(userId))
                {
                    label = roleName + " (" + userId + ")";
                }

                stages.Add(new ResolvedRequisitionFlowStageVm
                {
                    StageNumber = i + 1,
                    RoleId = roleId,
                    RoleName = roleName,
                    UserId = userId,
                    UserName = userName,
                    DisplayLabel = label
                });
            }

            var summary = stageRoles.Count == 0
                ? "No approvers on this path (submit may auto-approve)."
                : string.Join(" → ", stages.Select(s => s.DisplayLabel));

            return new ResolvedRequisitionFlowPreviewVm
            {
                HasApprovers = stageRoles.Count > 0,
                SourceKind = detailed.SourceKind,
                SourceLabel = detailed.SourceLabel,
                Summary = summary,
                Stages = stages
            };
        }

        private static string BuildCannotApproveReason(PurchaseRequestDetailVm model, string currentUserId, int? currentRoleId)
        {
            if (model == null)
            {
                return "You are not authorized to act on this stage.";
            }

            if (!string.IsNullOrWhiteSpace(model.CurrentStageUserId)
                && !string.Equals(model.CurrentStageUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                var who = string.IsNullOrWhiteSpace(model.CurrentStageUserName)
                    ? "another user"
                    : model.CurrentStageUserName;
                return "This stage is pinned to " + who + ".";
            }

            if (model.CurrentStageRoleId.HasValue
                && (!currentRoleId.HasValue || currentRoleId.Value != model.CurrentStageRoleId.Value))
            {
                var role = string.IsNullOrWhiteSpace(model.CurrentStageRoleName)
                    ? "the required role"
                    : model.CurrentStageRoleName;
                return "Your role is not the current stage approver (" + role + ").";
            }

            return "You are not authorized to act on this stage.";
        }

        private SelectList BuildOrderByUserSelectList(int? departmentId, string selectedUserId)
        {
            if (!departmentId.HasValue || departmentId.Value <= 0)
            {
                return new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
            }

            var orgId = ResolveCurrentOrganizationId();
            var users = orgId.HasValue
                ? BuildReferenceDataCache().GetUsersForDropdown(orgId.Value, departmentId)
                : BuildUserService().GetAll().Where(x => x.IsActive && x.DepartmentId == departmentId).ToList();
            var items = users
                .OrderBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .Select(x => new SelectListItem
                {
                    Value = x.Id,
                    Text = BuildUserLabel(x)
                })
                .ToList();
            return new SelectList(items, "Value", "Text", selectedUserId);
        }

        private string ResolveSelectedTargetAssetLabel(int? assetId)
        {
            if (!assetId.HasValue || assetId.Value <= 0)
            {
                return string.Empty;
            }

            var page = BuildAssetService().GetAssetListPage(
                new AssetFilterVm { OrganizationWide = true },
                "tag",
                "asc",
                1,
                500);
            var match = page.Items.FirstOrDefault(x => x.Id == assetId.Value);
            return match == null ? string.Empty : FormatTargetAssetLabel(match);
        }

        private static string FormatAssetStatusLabel(AssetStatus status)
        {
            return status.ToString()
                .Replace("AwaitingApproval", "Pending Approval")
                .Replace("InStore", "In Store");
        }

        private IList<SelectListItem> GetTargetAssetOptions()
        {
            var page = BuildAssetService().GetAssetListPage(
                new AssetFilterVm { OrganizationWide = true },
                "tag",
                "asc",
                1,
                500);

            return page.Items
                .OrderBy(x => x.AssetTag)
                .ThenBy(x => x.AssetName)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = FormatTargetAssetLabel(x)
                })
                .ToList();
        }

        private static string FormatTargetAssetLabel(AssetListVm asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var label = string.IsNullOrWhiteSpace(asset.AssetTag)
                ? asset.AssetName
                : asset.AssetTag + " - " + asset.AssetName;
            if (!string.IsNullOrWhiteSpace(asset.DepartmentName))
            {
                label += " · " + asset.DepartmentName;
            }

            return label;
        }

        private static string FormatTargetAssetItemDescription(AssetListVm asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(asset.AssetTag) && !string.IsNullOrWhiteSpace(asset.AssetName))
            {
                return asset.AssetTag + " - " + asset.AssetName;
            }

            if (!string.IsNullOrWhiteSpace(asset.AssetName))
            {
                return asset.AssetName;
            }

            return asset.AssetTag ?? string.Empty;
        }

        private Dictionary<string, int> BuildInStockQuantityLookup(IList<AssetListVm> items)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            if (items == null || items.Count == 0)
            {
                return lookup;
            }

            var stockService = BuildAssetStockService();
            foreach (var group in items.GroupBy(BuildInStockQuantityKey))
            {
                if (lookup.ContainsKey(group.Key))
                {
                    continue;
                }

                var sample = group.First();
                if (sample.AssetSubTypeId.HasValue && sample.AssetSubTypeId.Value > 0)
                {
                    lookup[group.Key] = stockService.GetAvailableQuantity(sample.AssetSubTypeId.Value, sample.DepartmentId);
                    continue;
                }

                if (sample.AssetTypeId <= 0)
                {
                    lookup[group.Key] = sample.CurrentStatus == AssetStatus.InStore ? 1 : 0;
                    continue;
                }

                lookup[group.Key] = BuildAssetService().CountAssets(new AssetFilterVm
                {
                    OrganizationWide = true,
                    AssetTypeId = sample.AssetTypeId,
                    DepartmentId = sample.DepartmentId,
                    Status = AssetStatus.InStore
                });
            }

            return lookup;
        }

        private static int ResolveInStockQuantity(AssetListVm asset, IDictionary<string, int> lookup)
        {
            if (asset == null || lookup == null)
            {
                return 0;
            }

            int quantity;
            return lookup.TryGetValue(BuildInStockQuantityKey(asset), out quantity) ? quantity : 0;
        }

        private static string BuildInStockQuantityKey(AssetListVm asset)
        {
            var departmentId = asset.DepartmentId.GetValueOrDefault(0);
            if (asset.AssetSubTypeId.HasValue && asset.AssetSubTypeId.Value > 0)
            {
                return "sub:" + departmentId + ":" + asset.AssetSubTypeId.Value;
            }

            return "type:" + departmentId + ":" + asset.AssetTypeId;
        }

        private void EnrichUserNames(PurchaseRequestDetailVm model)
        {
            var requester = BuildUserService().GetById(model.RequestedById);
            model.RequestedByName = requester == null ? model.RequestedById : BuildUserLabel(requester);
            if (!string.IsNullOrWhiteSpace(model.OrderByUserId))
            {
                var orderBy = BuildUserService().GetById(model.OrderByUserId);
                model.OrderByUserName = orderBy == null ? model.OrderByUserId : BuildUserLabel(orderBy);
            }
        }

        private void SaveOptionalAttachment(int purchaseRequestId, HttpPostedFileBase attachment)
        {
            if (attachment == null || attachment.ContentLength <= 0 || _storage == null)
            {
                return;
            }

            var extension = Path.GetExtension(attachment.FileName);
            if (string.IsNullOrWhiteSpace(extension)
                || !AllowedAttachmentExtensions.Contains(extension.ToLowerInvariant())
                || attachment.ContentLength > MaxAttachmentSizeBytes)
            {
                TempData["Error"] = "Attachment was skipped because the file type or size is not allowed.";
                return;
            }

            var storedFileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            using (var stream = attachment.InputStream)
            {
                var relativePath = _storage.Save(
                    stream,
                    storedFileName,
                    attachment.ContentType,
                    "purchase-requests/" + purchaseRequestId);
                _purchaseRequestService.SaveAttachment(purchaseRequestId, new PurchaseRequestAttachmentInfo
                {
                    FileName = Path.GetFileName(attachment.FileName),
                    FilePath = relativePath,
                    ContentType = attachment.ContentType,
                    FileSizeBytes = attachment.ContentLength
                });
            }
        }
    }
}
