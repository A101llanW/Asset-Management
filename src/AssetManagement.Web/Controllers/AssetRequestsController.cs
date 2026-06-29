using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.ViewModels;
using AssetManagement.Web.Security;
using System.Collections.Generic;

namespace AssetManagement.Web.Controllers
{
    public class AssetRequestsController : BaseController
    {
        private readonly IAssetRequestService _assetRequestService;
        private readonly IAssetService _assetService;

        public AssetRequestsController()
        {
            _assetRequestService = BuildAssetRequestService();
            _assetService = BuildAssetService();
        }

        [PermissionAuthorize("Assets.Request")]
        public ActionResult Index(AssetRequestFilterVm filter, string sort = "created", string direction = "desc", int page = 1, int pageSize = 10)
        {
            if (filter == null)
            {
                filter = new AssetRequestFilterVm();
            }

            filter = ListRoleDefaultsHelper.ApplyAssetRequestListDefaults(
                filter,
                GetCurrentUserProfile(),
                HasPermission("Assets.Request.Approve"),
                IsCurrentUserSuperAdmin());

            var pageModel = _assetRequestService.GetRequests(filter, sort, direction, page, pageSize);
            ViewBag.Departments = BuildDepartmentSelectList(filter.DepartmentId);
            SetListSortViewBag(sort, direction);
            return View(ToListPage(pageModel));
        }

        [PermissionAuthorize("Assets.Request")]
        public ActionResult Create()
        {
            var userId = User.GetUserId();
            var user = BuildUserService().GetById(userId);
            var model = new AssetRequestCreateVm
            {
                RequestForSelf = true,
                DepartmentId = user == null ? null : user.DepartmentId
            };
            ApplyLockedUserDepartment(user?.DepartmentId, deptId => model.DepartmentId = deptId);
            PopulateLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Request")]
        public ActionResult Create([Bind(Prefix = "")] AssetRequestCreateVm viewModel)
        {
            if (viewModel == null)
            {
                viewModel = new AssetRequestCreateVm();
            }

            viewModel.RequestForSelf = true;
            var user = GetCurrentUserProfile();
            ApplyLockedUserDepartment(user?.DepartmentId, deptId => viewModel.DepartmentId = deptId);
            var requesterId = User.GetUserId();
            PopulateLookups(viewModel);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var id = _assetRequestService.Submit(viewModel, requesterId);
                TempData["Message"] = "Asset request submitted successfully.";
                return RedirectToAction("Details", new { id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        [PermissionAuthorize("Assets.Request")]
        public ActionResult Details(int id)
        {
            var model = _assetRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            if (!HasPermission("Assets.Request.Approve") && model.RequestedById != User.GetUserId())
            {
                return new HttpStatusCodeResult(403);
            }

            EnrichRequestNames(model);
            ViewBag.CanApprove = HasPermission("Assets.Request.Approve") && model.Status == AssetRequestStatus.Pending;
            ViewBag.CanFulfill = HasPermission("Assets.Request.Approve") && model.Status == AssetRequestStatus.Approved;
            ViewBag.CanCreateRequisition = HasPermission("Purchases.Create");

            int inStoreCount = 0;
            if (model.DepartmentId.HasValue && model.CategoryId.HasValue)
            {
                var stockFilter = new AssetFilterVm
                {
                    Status = AssetStatus.InStore,
                    DepartmentId = model.DepartmentId,
                    CategoryId = model.CategoryId,
                    OrganizationWide = true
                };
                inStoreCount = _assetService.GetAssetListPage(stockFilter, "name", "asc", 1, 1).TotalCount;
            }

            ViewBag.InStoreAssetCount = inStoreCount;

            if (ViewBag.CanFulfill == true)
            {
                var fulfillFilter = new AssetFilterVm
                {
                    Status = AssetStatus.InStore,
                    OrganizationWide = true
                };
                if (model.DepartmentId.HasValue)
                {
                    fulfillFilter.DepartmentId = model.DepartmentId;
                }

                if (model.CategoryId.HasValue)
                {
                    fulfillFilter.CategoryId = model.CategoryId;
                }

                var availableAssets = _assetService.GetAssetListPage(fulfillFilter, "name", "asc", 1, 500).Items
                    .OrderBy(x => x.AssetName)
                    .Select(x => new { x.Id, Name = x.AssetName })
                    .ToList();
                ViewBag.AvailableAssets = new SelectList(
                    availableAssets,
                    "Id",
                    "Name",
                    model.RequestedAssetId);
                ViewBag.AssigneeUsers = BuildActiveUserSelectList(
                    model.RequestedById,
                    model.DepartmentId);
                ViewBag.DefaultAssigneeUserId = model.RequestedById;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Request.Approve")]
        public ActionResult Approve(int id, string notes)
        {
            try
            {
                _assetRequestService.Approve(id, User.GetUserId(), notes);
                TempData["Message"] = "Asset request approved.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Request.Approve")]
        public ActionResult Reject(int id, string notes)
        {
            try
            {
                _assetRequestService.Reject(id, User.GetUserId(), notes);
                TempData["Message"] = "Asset request rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Request.Approve")]
        public ActionResult Fulfill([Bind(Prefix = "")] AssetRequestFulfillVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index");
            }

            var request = _assetRequestService.GetById(viewModel.RequestId);
            if (request != null && request.DepartmentId.HasValue)
            {
                viewModel.ToDepartmentId = request.DepartmentId;
                if (!ValidateUserBelongsToDepartment(viewModel.ToUserId, request.DepartmentId))
                {
                    TempData["Error"] = "Assignee must belong to the request department.";
                    return RedirectToAction("Details", new { id = viewModel.RequestId });
                }
            }

            try
            {
                _assetRequestService.Fulfill(
                    viewModel.RequestId,
                    viewModel.AssetId,
                    User.GetUserId(),
                    new AssetAssignmentVm
                    {
                        AssetId = viewModel.AssetId,
                        ToUserId = viewModel.ToUserId,
                        ToDepartmentId = viewModel.ToDepartmentId,
                        HandoverNotes = viewModel.HandoverNotes,
                        AssignmentType = AssignmentType.Permanent.ToString(),
                        HandedOverById = User.GetUserId(),
                        ReceivedById = viewModel.ToUserId
                    });
                TempData["Message"] = "Asset request fulfilled and asset assigned.";
                return RedirectToTenantAware("AssetRequests", "Details", new { id = viewModel.RequestId });
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = viewModel.RequestId });
            }
        }

        [PermissionAuthorize("Assets.Request")]
        public JsonResult AvailableAssets(int? departmentId, int? categoryId)
        {
            if (!departmentId.HasValue || !categoryId.HasValue)
            {
                return Json(new object[0], JsonRequestBehavior.AllowGet);
            }

            var filter = new AssetFilterVm
            {
                DepartmentId = departmentId,
                CategoryId = categoryId,
                Status = AssetStatus.InStore,
                OrganizationWide = true
            };

            var page = _assetService.GetAssetListPage(filter, "name", "asc", 1, 500);

            var items = page.Items
                .OrderBy(x => x.AssetName)
                .Select(x => new { id = x.Id, name = x.AssetTag + " - " + x.AssetName })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        private void PopulateLookups(AssetRequestCreateVm model)
        {
            var lockDepartment = GetCurrentUserDepartmentId().HasValue && !IsCurrentUserSuperAdmin();
            ViewBag.LockDepartment = lockDepartment;
            ViewBag.DepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                model == null ? GetCurrentUserDepartmentId() : model.DepartmentId,
                BuildDepartmentService().GetAll().Where(x => x.IsActive).ToList());
            ViewBag.Departments = BuildDepartmentSelectList(model == null ? null : model.DepartmentId);
            ViewBag.Categories = BuildCategorySelectList(model == null ? null : model.CategoryId);
        }

        private void EnrichRequestNames(AssetRequestDetailsVm model)
        {
            var requester = BuildUserService().GetById(model.RequestedById);
            model.RequestedByName = requester == null ? model.RequestedById : BuildUserLabel(requester);
            if (!string.IsNullOrWhiteSpace(model.ReviewedByName))
            {
                var reviewer = BuildUserService().GetById(model.ReviewedByName);
                if (reviewer != null)
                {
                    model.ReviewedByName = BuildUserLabel(reviewer);
                }
            }
        }

        private bool HasPermission(string permissionCode)
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), permissionCode);
        }

        private static ListPageViewModel<AssetRequestListVm> ToListPage(AssetRequestListPageVm source)
        {
            return new ListPageViewModel<AssetRequestListVm>
            {
                Items = source.Items.ToList(),
                Search = source.Search,
                Sort = source.Sort,
                Direction = source.Direction,
                Page = source.Page,
                PageSize = source.PageSize,
                TotalCount = source.TotalCount
            };
        }
    }
}
