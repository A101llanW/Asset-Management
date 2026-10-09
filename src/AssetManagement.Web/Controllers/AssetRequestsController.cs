using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Queries;
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
            if (viewModel.TargetKind != AssetRequestTargetKind.Room)
            {
                viewModel.TargetKind = AssetRequestTargetKind.User;
                viewModel.TargetDepartmentId = null;
            }
            else if (!TargetRepository.IsAvailable())
            {
                ModelState.AddModelError("TargetKind", "Delivering to a room is not available yet (database migration 097 has not been applied).");
            }
            else if (!viewModel.TargetDepartmentId.HasValue
                || !viewModel.DepartmentId.HasValue
                || !GetRoomTargetOptions(viewModel.DepartmentId).Any(x => x.Key == viewModel.TargetDepartmentId.Value))
            {
                ModelState.AddModelError("TargetDepartmentId", "Choose a room or class inside the selected department.");
            }

            PopulateLookups(viewModel);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var id = _assetRequestService.Submit(viewModel, requesterId);
                if (viewModel.TargetKind == AssetRequestTargetKind.Room)
                {
                    var orgId = ResolveCurrentOrganizationId();
                    if (!orgId.HasValue || !TargetRepository.Set(id, orgId.Value, AssetRequestTargetRepository.TargetRoom, viewModel.TargetDepartmentId))
                    {
                        TempData["Error"] = "The request was submitted, but its delivery room could not be saved; it will be delivered to you.";
                    }
                }

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
            ApplyTarget(model);
            ViewBag.CanApprove = HasPermission("Assets.Request.Approve") && model.Status == AssetRequestStatus.Pending;
            ViewBag.CanFulfill = HasPermission("Assets.Request.Approve") && model.Status == AssetRequestStatus.Approved;
            ViewBag.CanCreateRequisition = HasPermission("Purchases.Create");

            int inStoreCount = 0;
            if (model.DepartmentId.HasValue)
            {
                var stockService = BuildAssetStockService();
                if (model.RequestedAssetId.HasValue && model.RequestedAssetId.Value > 0)
                {
                    var requestedAsset = UnitOfWork.Repository<Asset>().GetById(model.RequestedAssetId.Value);
                    if (requestedAsset != null && requestedAsset.AssetSubTypeId.HasValue)
                    {
                        inStoreCount = stockService.GetAvailableQuantity(
                            requestedAsset.AssetSubTypeId.Value,
                            model.DepartmentId);
                    }
                }

                if (inStoreCount == 0 && model.CategoryId.HasValue)
                {
                    // rel1009g: the department and its descendant classes/rooms.
                    inStoreCount = GetInStoreAssetsInSubtree(model.DepartmentId.Value, model.CategoryId).Count;
                }
            }

            ViewBag.InStoreAssetCount = inStoreCount;

            if (ViewBag.CanFulfill == true)
            {
                IList<AssetListVm> candidates;
                if (model.DepartmentId.HasValue)
                {
                    candidates = GetInStoreAssetsInSubtree(model.DepartmentId.Value, model.CategoryId);
                }
                else
                {
                    candidates = _assetService.GetAssetListPage(
                        new AssetFilterVm { Status = AssetStatus.InStore, CategoryId = model.CategoryId, OrganizationWide = true },
                        "name",
                        "asc",
                        1,
                        500).Items.ToList();
                }

                var availableAssets = ToAssetOptions(candidates)
                    .Select(x => new { Id = x.Key, Name = x.Value })
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
                if (model.TargetKind == AssetRequestTargetKind.Room && model.TargetDepartmentId.HasValue)
                {
                    var room = BuildDepartmentService().GetById(model.TargetDepartmentId.Value);
                    var pic = DepartmentTreeHelper.ResolvePicUserId(room);
                    var picUser = string.IsNullOrWhiteSpace(pic) ? null : BuildUserService().GetById(pic);
                    ViewBag.TargetPicUserName = string.IsNullOrWhiteSpace(pic) ? null : (picUser == null ? pic : BuildUserLabel(picUser));
                    ViewBag.TargetPicRoleLabel = room != null && room.DepartmentKind == DepartmentKind.Class ? "class teacher" : "room custodian";
                }
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
            if (request == null)
            {
                return HttpNotFound();
            }

            ApplyTarget(request);
            var assignmentType = AssignmentType.Permanent;
            if (request.TargetKind == AssetRequestTargetKind.Room && request.TargetDepartmentId.HasValue)
            {
                // Room target: assign to the room like Assignments (person in charge or department pool; never a silent pool).
                viewModel.ToDepartmentId = request.TargetDepartmentId;
                var pic = DepartmentTreeHelper.ResolvePicUserId(BuildDepartmentService().GetById(request.TargetDepartmentId.Value));
                var choice = (viewModel.PicPlacementChoice ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(pic))
                {
                    viewModel.ToUserId = null;
                    assignmentType = AssignmentType.DepartmentPool;
                }
                else if (string.Equals(choice, "Pic", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.ToUserId = pic;
                }
                else if (string.Equals(choice, "Pool", StringComparison.OrdinalIgnoreCase))
                {
                    viewModel.ToUserId = null;
                    assignmentType = AssignmentType.DepartmentPool;
                }
                else
                {
                    TempData["Error"] = "This room has a person in charge. Choose whether to put the asset under that person or leave it as a room pool.";
                    return RedirectToAction("Details", new { id = viewModel.RequestId });
                }
            }
            else if (request.DepartmentId.HasValue)
            {
                viewModel.ToDepartmentId = request.DepartmentId;
                if (!ValidateUserBelongsToDepartment(viewModel.ToUserId, request.DepartmentId))
                {
                    TempData["Error"] = "Assignee must belong to the request department.";
                    return RedirectToAction("Details", new { id = viewModel.RequestId });
                }
            }

            if (request.DepartmentId.HasValue)
            {
                var asset = UnitOfWork.Repository<Asset>().GetById(viewModel.AssetId);
                var subtree = DepartmentTreeHelper.GetSelfAndDescendantIds(request.DepartmentId.Value, GetActiveDepartments());
                if (asset == null || !asset.DepartmentId.HasValue || !subtree.Contains(asset.DepartmentId.Value))
                {
                    TempData["Error"] = "Pick an in-store asset from the request department or one of its rooms.";
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
                        AssignmentType = assignmentType.ToString(),
                        DepartmentDisposition = CrossDepartmentDisposition.Normalize(viewModel.DepartmentDisposition),
                        HandedOverById = User.GetUserId()
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

            // rel1009g: in-store assets in the department and every descendant class/room.
            var items = ToAssetOptions(GetInStoreAssetsInSubtree(departmentId.Value, categoryId))
                .Select(x => new { id = x.Key, name = x.Value })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        [PermissionAuthorize("Assets.Request")]
        public JsonResult RoomTargets(int? departmentId)
        {
            var items = GetRoomTargetOptions(departmentId)
                .Select(x => new { id = x.Key, name = x.Value })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        private const int MaxSubtreeAssets = 500;

        private IList<AssetListVm> GetInStoreAssetsInSubtree(int departmentId, int? categoryId)
        {
            var result = new List<AssetListVm>();
            var seen = new HashSet<int>();
            foreach (var id in DepartmentTreeHelper.GetSelfAndDescendantIds(departmentId, GetActiveDepartments()))
            {
                var filter = new AssetFilterVm
                {
                    DepartmentId = id,
                    CategoryId = categoryId,
                    Status = AssetStatus.InStore,
                    OrganizationWide = true
                };
                foreach (var item in _assetService.GetAssetListPage(filter, "name", "asc", 1, MaxSubtreeAssets).Items)
                {
                    if (seen.Add(item.Id))
                    {
                        result.Add(item);
                    }
                }

                if (result.Count >= MaxSubtreeAssets)
                {
                    break;
                }
            }

            return result;
        }

        private IList<KeyValuePair<int, string>> ToAssetOptions(IEnumerable<AssetListVm> assets)
        {
            var departments = GetActiveDepartments();
            return (assets ?? Enumerable.Empty<AssetListVm>())
                .Select(x => new KeyValuePair<int, string>(
                    x.Id,
                    DepartmentTreeHelper.FormatAssetOption(
                        x.AssetTag,
                        x.SerialNumber,
                        x.AssetName,
                        x.DepartmentId.HasValue ? (departments.FirstOrDefault(d => d.Id == x.DepartmentId.Value)?.Name ?? x.DepartmentName) : x.DepartmentName)))
                .OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Active Class/Room departments in the request department's subtree, labelled with their path.</summary>
        private IList<KeyValuePair<int, string>> GetRoomTargetOptions(int? departmentId)
        {
            if (!departmentId.HasValue)
            {
                return new List<KeyValuePair<int, string>>();
            }

            var departments = GetActiveDepartments();
            var subtree = new HashSet<int>(DepartmentTreeHelper.GetSelfAndDescendantIds(departmentId.Value, departments));
            return departments
                .Where(x => subtree.Contains(x.Id) && DepartmentTreeHelper.IsRoomTargetKind(x.DepartmentKind))
                .Select(x => new KeyValuePair<int, string>(x.Id, DepartmentTreeHelper.BuildPathLabel(x.Id, departments)))
                .OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ApplyTarget(AssetRequestDetailsVm model)
        {
            var orgId = ResolveCurrentOrganizationId();
            if (model == null || !orgId.HasValue)
            {
                return;
            }

            int kind;
            int? targetDepartmentId;
            TargetRepository.Get(model.Id, orgId.Value, out kind, out targetDepartmentId);
            model.TargetKind = kind == AssetRequestTargetRepository.TargetRoom ? AssetRequestTargetKind.Room : AssetRequestTargetKind.User;
            model.TargetDepartmentId = targetDepartmentId;
            if (targetDepartmentId.HasValue)
            {
                var departments = GetActiveDepartments();
                var label = DepartmentTreeHelper.BuildPathLabel(targetDepartmentId.Value, departments);
                if (string.IsNullOrWhiteSpace(label))
                {
                    var dept = BuildDepartmentService().GetById(targetDepartmentId.Value);
                    label = dept == null ? ("Department #" + targetDepartmentId.Value) : dept.Name;
                }

                model.TargetDepartmentName = label;
            }
        }

        private AssetRequestTargetRepository _targetRepository;

        private AssetRequestTargetRepository TargetRepository
        {
            get
            {
                if (_targetRepository == null)
                {
                    _targetRepository = new AssetRequestTargetRepository(
                        DependencyResolver.Current.GetService<AssetManagement.Infrastructure.Persistence.ISqlConnectionFactory>());
                }

                return _targetRepository;
            }
        }

        private void PopulateLookups(AssetRequestCreateVm model)
        {
            var lockDepartment = GetCurrentUserDepartmentId().HasValue && !IsCurrentUserSuperAdmin();
            ViewBag.LockDepartment = lockDepartment;
            ViewBag.DepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                model == null ? GetCurrentUserDepartmentId() : model.DepartmentId,
                GetActiveDepartments());
            ViewBag.Departments = BuildDepartmentSelectList(model == null ? null : model.DepartmentId);
            ViewBag.Categories = BuildCategorySelectList(model == null ? null : model.CategoryId);
            ViewBag.RoomTargetsAvailable = TargetRepository.IsAvailable();
            ViewBag.RoomTargets = new SelectList(
                GetRoomTargetOptions(model == null ? null : model.DepartmentId).Select(x => new { Id = x.Key, Name = x.Value }),
                "Id",
                "Name",
                model == null ? null : model.TargetDepartmentId);
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
