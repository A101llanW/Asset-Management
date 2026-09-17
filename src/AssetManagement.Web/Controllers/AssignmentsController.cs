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
        private const int BatchAssignMaxRows = 100;

        private readonly IAssignmentService _assignmentService;
        private readonly IAssetService _assetService;

        public AssignmentsController()
        {
            _assignmentService = BuildAssignmentService();
            _assetService = BuildAssetService();
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
        public ActionResult Create(int? assetId)
        {
            if (!assetId.HasValue)
            {
                TempData["Error"] = "Select an asset to assign.";
                return RedirectToAction("Index", "Assets");
            }

            var asset = UnitOfWork.Repository<Asset>().GetById(assetId.Value);
            if (asset == null)
            {
                return HttpNotFound();
            }

            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                TempData["Error"] = scopeError;
                return RedirectToAssetDetails(assetId.Value);
            }

            if (!AssetCustodyRules.CanAssign(asset.CurrentStatus))
            {
                TempData["Error"] = AssetCustodyRules.GetAssignBlockedMessage(asset.CurrentStatus);
                return RedirectToAssetDetails(assetId.Value);
            }

            var model = new AssetAssignmentVm
            {
                AssetId = assetId.Value,
                AssignedDate = DateTime.UtcNow,
                AssignmentType = AssignmentType.Permanent.ToString(),
                HandedOverById = CurrentUserContext.UserId,
                ConditionBeforeHandover = asset.Condition.ToString()
            };

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId => model.ToDepartmentId = deptId);
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId.Value);
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

            if (!string.IsNullOrWhiteSpace(viewModel.ReceivedById) && !ValidateUserBelongsToDepartment(viewModel.ReceivedById, viewModel.ToDepartmentId))
            {
                ModelState.AddModelError("ReceivedById", "Received-by user must belong to the target department.");
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

        [PermissionAuthorize("Assets.Assign")]
        public ActionResult BatchCreate(string assetIds, AssetFilterVm filter, bool fromFilter = false, string returnUrl = null)
        {
            var rows = LoadBatchAssignRows(assetIds, filter, fromFilter);
            if (rows.Count == 0)
            {
                TempData["Error"] = "No assignable assets were found for batch assignment.";
                return RedirectToAction("Index", "Assets");
            }

            var model = new BatchAssignPageVm
            {
                Rows = rows,
                ReturnUrl = ResolveBatchAssignReturnUrl(returnUrl, filter, fromFilter)
            };

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId => model.ToDepartmentId = deptId);
            PopulateBatchLookups(model);
            return View("BatchCreate", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Assign")]
        public ActionResult BatchCreate(BatchAssignPageVm model)
        {
            if (model == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            model.Rows = model.Rows ?? new List<BatchAssignRowVm>();
            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId => model.ToDepartmentId = deptId);

            var assignableRows = model.Rows.Where(x => x != null && x.CanAssign).ToList();
            if (assignableRows.Count == 0)
            {
                ModelState.AddModelError("", "No assignable assets remain on this page.");
            }

            foreach (var row in assignableRows)
            {
                if (string.IsNullOrWhiteSpace(row.ToUserId))
                {
                    continue;
                }

                if (!ValidateUserBelongsToDepartment(row.ToUserId, model.ToDepartmentId))
                {
                    ModelState.AddModelError("", "Selected user does not belong to the target department for asset " + (row.AssetTag ?? row.AssetId.ToString()) + ".");
                }
            }

            PopulateBatchLookups(model);
            if (!ModelState.IsValid)
            {
                return View("BatchCreate", model);
            }

            try
            {
                var request = new BatchAssignRequestVm
                {
                    ToDepartmentId = model.ToDepartmentId,
                    HandedOverById = CurrentUserContext.UserId,
                    HandoverNotes = model.HandoverNotes,
                    Items = assignableRows
                        .Where(x => !string.IsNullOrWhiteSpace(x.ToUserId))
                        .Select(x => new BatchAssignItemVm
                        {
                            AssetId = x.AssetId,
                            ToUserId = x.ToUserId
                        })
                        .ToList()
                };

                if (request.Items.Count == 0)
                {
                    ModelState.AddModelError("", "Select at least one custodian before saving.");
                    return View("BatchCreate", model);
                }

                model.LastResult = _assignmentService.BatchAssign(request);
                RefreshBatchRowState(model);
                TempData["Message"] = "Batch assignment completed: "
                    + model.LastResult.ProcessedCount + " assigned, "
                    + model.LastResult.SkippedCount + " skipped.";
                return View("BatchCreate", model);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("BatchCreate", model);
            }
        }

        private IList<BatchAssignRowVm> LoadBatchAssignRows(string assetIds, AssetFilterVm filter, bool fromFilter)
        {
            if (!string.IsNullOrWhiteSpace(assetIds))
            {
                return BuildBatchRowsFromIds(ParseAssetIds(assetIds));
            }

            if (fromFilter)
            {
                filter = ListRoleDefaultsHelper.ApplyAssetListDefaults(
                    filter ?? new AssetFilterVm(),
                    GetCurrentUserProfile(),
                    BuildAuthorizationService().HasPermission(User.GetUserId(), "Assets.Assign"),
                    IsCurrentUserSuperAdmin());
                filter.UnassignedOnly = true;
                if (!filter.Status.HasValue)
                {
                    filter.Status = AssetStatus.InStore;
                }

                var page = _assetService.GetAssetListPage(filter, "tag", "asc", 1, BatchAssignMaxRows);
                return page.Items
                    .Select(x => BuildBatchRowFromAsset(UnitOfWork.Repository<Asset>().GetById(x.Id)))
                    .Where(x => x != null)
                    .ToList();
            }

            return new List<BatchAssignRowVm>();
        }

        private static IList<int> ParseAssetIds(string assetIds)
        {
            return (assetIds ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    int id;
                    return int.TryParse(x.Trim(), out id) ? id : 0;
                })
                .Where(x => x > 0)
                .Distinct()
                .Take(BatchAssignMaxRows)
                .ToList();
        }

        private IList<BatchAssignRowVm> BuildBatchRowsFromIds(IEnumerable<int> ids)
        {
            var rows = new List<BatchAssignRowVm>();
            foreach (var id in ids)
            {
                var row = BuildBatchRowFromAsset(UnitOfWork.Repository<Asset>().GetById(id));
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private BatchAssignRowVm BuildBatchRowFromAsset(Asset asset)
        {
            if (asset == null)
            {
                return null;
            }

            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                return new BatchAssignRowVm
                {
                    AssetId = asset.Id,
                    AssetTag = asset.AssetTag,
                    SerialNumber = asset.SerialNumber,
                    AssetName = asset.AssetName,
                    CanAssign = false,
                    BlockReason = scopeError
                };
            }

            var canAssign = AssetCustodyRules.CanAssign(asset.CurrentStatus);
            var blockReason = canAssign
                ? null
                : AssetCustodyRules.GetAssignBlockedMessage(asset.CurrentStatus);

            if (canAssign && !string.IsNullOrWhiteSpace(asset.CurrentCustodianId))
            {
                canAssign = false;
                blockReason = "Asset already has a custodian. Use Transfer instead.";
            }

            return new BatchAssignRowVm
            {
                AssetId = asset.Id,
                AssetTag = asset.AssetTag,
                SerialNumber = asset.SerialNumber,
                AssetName = asset.AssetName,
                CanAssign = canAssign,
                BlockReason = blockReason
            };
        }

        private void RefreshBatchRowState(BatchAssignPageVm model)
        {
            if (model?.Rows == null)
            {
                return;
            }

            for (var i = 0; i < model.Rows.Count; i++)
            {
                var current = model.Rows[i];
                var refreshed = BuildBatchRowFromAsset(UnitOfWork.Repository<Asset>().GetById(current.AssetId));
                if (refreshed == null)
                {
                    continue;
                }

                refreshed.ToUserId = current.ToUserId;
                model.Rows[i] = refreshed;
            }
        }

        private string ResolveBatchAssignReturnUrl(string returnUrl, AssetFilterVm filter, bool fromFilter)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            if (fromFilter && filter != null)
            {
                return Url.Action("Index", "Assets", new
                {
                    Status = filter.Status,
                    DepartmentId = filter.DepartmentId,
                    UnassignedOnly = filter.UnassignedOnly,
                    Search = filter.Search
                });
            }

            return Url.Action("Index", "Assets");
        }

        private void PopulateBatchLookups(BatchAssignPageVm model)
        {
            var activeUsers = GetActiveUsers().ToList();
            var lockToDepartment = !IsCurrentUserSuperAdmin() && GetCurrentUserDepartmentId().HasValue;
            var toDepartmentId = model?.ToDepartmentId ?? (lockToDepartment ? GetCurrentUserDepartmentId() : null);
            if (lockToDepartment && model != null && !model.ToDepartmentId.HasValue)
            {
                model.ToDepartmentId = GetCurrentUserDepartmentId();
                toDepartmentId = model.ToDepartmentId;
            }

            ViewBag.Users = BuildActiveUserSelectList(null, toDepartmentId);
            ViewBag.Departments = BuildDepartmentSelectList(toDepartmentId);
            ViewBag.AllDepartments = BuildDepartmentSelectList(model?.ToDepartmentId);
            ViewBag.LockToDepartment = lockToDepartment;
            ViewBag.ToDepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                toDepartmentId,
                GetActiveDepartments());

            var lockedFields = new List<WorkflowLockedFieldVm>();
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
                        UserFieldId = "batch-user-select",
                        RequireDepartmentForUsers = true
                    }
                },
                lockedFields));
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
                GetActiveDepartments());

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
