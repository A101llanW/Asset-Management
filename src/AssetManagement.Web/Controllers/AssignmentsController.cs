using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    public class AssignmentsController : BaseController
    {
        private readonly IAssignmentService _assignmentService;
        public AssignmentsController()
        {
            _assignmentService = BuildAssignmentService();
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult List(AssignmentFilterVm filter, string sort = "date", string direction = "desc", int page = 1, int pageSize = 10)
        {
            filter = ListRoleDefaultsHelper.ApplyAssignmentListDefaults(
                filter,
                GetCurrentUserProfile(),
                BuildAuthorizationService().HasPermission(User.GetUserId(), "Assets.Assign"),
                IsCurrentUserSuperAdmin());
            var pageModel = _assignmentService.GetAssignmentListPage(filter, sort, direction, page, pageSize);
            ViewBag.Departments = BuildDepartmentSelectList(filter?.DepartmentId);
            SetListSortViewBag(sort, direction);
            return View("List", ToAssignmentListPage(pageModel));
        }

        [PermissionAuthorize("Assets.View")]
        public ActionResult Index(int assetId)
        {
            ViewBag.AssetId = assetId;
            var users = BuildUserService().GetAll()
                .ToDictionary(x => x.Id, x => BuildUserLabel(x));
            var departments = BuildDepartmentService().GetAll()
                .ToDictionary(x => x.Id, x => x.Name);

            var assignments = _assignmentService.GetByAsset(assetId)
                .Select(x =>
                {
                    x.ToUserName = !string.IsNullOrWhiteSpace(x.ToUserId) && users.ContainsKey(x.ToUserId)
                        ? users[x.ToUserId]
                        : x.ToUserName;
                    x.ToDepartmentName = x.ToDepartmentId.HasValue && departments.ContainsKey(x.ToDepartmentId.Value)
                        ? departments[x.ToDepartmentId.Value]
                        : x.ToDepartmentName;
                    return x;
                })
                .ToList();

            return View(assignments);
        }

        [PermissionAuthorize("Assets.Assign")]
        public ActionResult Create(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                return HttpNotFound();
            }

            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                TempData["Error"] = scopeError;
                return RedirectToAssetDetails(assetId);
            }

            if (!AssetCustodyRules.CanAssign(asset.CurrentStatus))
            {
                TempData["Error"] = AssetCustodyRules.GetAssignBlockedMessage(asset.CurrentStatus);
                return RedirectToAssetDetails(assetId);
            }

            var model = new AssetAssignmentVm
            {
                AssetId = assetId,
                AssignedDate = DateTime.UtcNow,
                AssignmentType = AssignmentType.Permanent.ToString(),
                HandedOverById = CurrentUserContext.UserId,
                ConditionBeforeHandover = asset.Condition.ToString()
            };

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId => model.ToDepartmentId = deptId);
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Assign")]
        public ActionResult Create([Bind(Prefix = "")] AssetAssignmentVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            var asset = UnitOfWork.Repository<Asset>().GetById(viewModel.AssetId);
            if (asset == null)
            {
                return HttpNotFound();
            }

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId => viewModel.ToDepartmentId = deptId);
            viewModel.HandedOverById = CurrentUserContext.UserId;

            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                ModelState.AddModelError("", scopeError);
            }

            if (!string.IsNullOrWhiteSpace(viewModel.ToUserId) && !ValidateUserBelongsToDepartment(viewModel.ToUserId, viewModel.ToDepartmentId))
            {
                ModelState.AddModelError("ToUserId", "Selected user does not belong to the target department.");
            }

            PopulateLookups(viewModel);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                _assignmentService.Assign(viewModel);
                TempData["Message"] = "Asset assigned successfully.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        private static ListPageViewModel<AssignmentListVm> ToAssignmentListPage(AssignmentListPageVm source)
        {
            return new ListPageViewModel<AssignmentListVm>
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

        private void PopulateLookups(AssetAssignmentVm model)
        {
            var activeUsers = GetActiveUsers().ToList();
            var lockToDepartment = !IsCurrentUserSuperAdmin() && GetCurrentUserDepartmentId().HasValue;
            var toDepartmentId = model?.ToDepartmentId ?? (lockToDepartment ? GetCurrentUserDepartmentId() : null);
            if (lockToDepartment && model != null && !model.ToDepartmentId.HasValue)
            {
                model.ToDepartmentId = GetCurrentUserDepartmentId();
                toDepartmentId = model.ToDepartmentId;
            }

            ViewBag.Users = BuildActiveUserSelectList(model?.ToUserId, toDepartmentId);
            ViewBag.AllUsers = BuildActiveUserSelectList(model?.HandedOverById);
            ViewBag.Departments = BuildDepartmentSelectList(toDepartmentId);
            ViewBag.AllDepartments = BuildDepartmentSelectList(model?.ToDepartmentId);
            ViewBag.LockToDepartment = lockToDepartment;
            ViewBag.HandedOverByName = DepartmentUserWorkflowHelper.ResolveUserDisplayName(model?.HandedOverById, activeUsers);
            ViewBag.ToDepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                toDepartmentId,
                BuildDepartmentService().GetAll().Where(x => x.IsActive).ToList());

            var selectedType = string.IsNullOrWhiteSpace(model?.AssignmentType)
                ? AssignmentType.Permanent.ToString()
                : model.AssignmentType;
            var assignmentTypes = Enum.GetNames(typeof(AssignmentType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.AssignmentTypes = new SelectList(assignmentTypes, "Value", "Text", selectedType);
            ViewBag.ConditionOptions = BuildAssetConditionSelectList(model?.ConditionBeforeHandover);

            var lockedFields = new List<WorkflowLockedFieldVm>
            {
                new WorkflowLockedFieldVm { FieldId = "HandedOverById" }
            };
            if (lockToDepartment)
            {
                lockedFields.Add(new WorkflowLockedFieldVm { FieldId = "ToDepartmentId" });
            }

            SetWorkflowFormConfig(BuildWorkflowFormConfig(
                activeUsers,
                new[]
                {
                    new WorkflowDepartmentUserPairVm
                    {
                        DepartmentFieldId = "ToDepartmentId",
                        UserFieldId = "ToUserId",
                        RequireDepartmentForUsers = true
                    },
                    new WorkflowDepartmentUserPairVm
                    {
                        DepartmentFieldId = "ToDepartmentId",
                        UserFieldId = "ReceivedById",
                        RequireDepartmentForUsers = true
                    }
                },
                lockedFields));
        }
    }
}
