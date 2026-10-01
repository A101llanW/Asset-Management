using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Departments.View")]
    public class DepartmentsController : BaseController
    {
        private readonly IDepartmentService _departmentService;

        public DepartmentsController()
        {
            _departmentService = BuildDepartmentService();
        }

        public ActionResult Index(string search = null, string status = "active", string view = "tree")
        {
            var items = FilterBySearch(_departmentService.GetAll(), search, (x, term) =>
                (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Code ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term));

            switch ((status ?? "active").ToLowerInvariant())
            {
                case "all":
                    break;
                case "inactive":
                    items = items.Where(x => !x.IsActive);
                    break;
                default:
                    status = "active";
                    items = items.Where(x => x.IsActive);
                    break;
            }

            ViewBag.StatusFilter = status;
            ViewBag.ViewMode = string.Equals(view, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "tree";
            ViewBag.Search = search;
            var itemsList = items.OrderBy(x => x.Name).ToList();
            var visibleIds = new System.Collections.Generic.HashSet<int>(itemsList.Select(x => x.Id));
            ViewBag.TreeSections = _departmentService.GetTreeSections(itemsList)
                .Select(section => new DepartmentTreeSectionVm
                {
                    Title = section.Title,
                    Items = section.Items
                        .Where(item => DepartmentOrgHierarchyDisplay.IsNodeOrDescendantInSet(item, visibleIds))
                        .ToList()
                })
                .Where(section => section.Items.Any())
                .ToList();

            return View(itemsList);
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            var organizationId = ResolveCurrentOrganizationId();
            ViewBag.ActiveUserCount = organizationId.HasValue
                ? DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Queries.IUserAccountQueryRepository>()
                    .CountActiveUsersForDepartment(organizationId.Value, id)
                : 0;
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { DepartmentId = model.Id });
            var scopedDepartments = _departmentService.GetAll().ToList();
            if (model.DepartmentKind == DepartmentKind.Administrative && !model.ParentDepartmentId.HasValue)
            {
                ViewBag.SubDepartments = DepartmentOrgHierarchyDisplay.GetSubDepartmentsUnder(scopedDepartments, model.Id);
                ViewBag.DirectRooms = DepartmentOrgHierarchyDisplay.GetDirectRoomsUnderAdministrative(scopedDepartments, model.Id);
            }
            else if (model.DepartmentKind == DepartmentKind.SubDepartment)
            {
                ViewBag.ChildRooms = DepartmentOrgHierarchyDisplay.GetRoomsUnderSubDepartment(scopedDepartments, model.Id);
            }

            return View(model);
        }

        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(string setupMode = null, int? parentId = null, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            var createContext = ResolveCreateContext(setupMode, parentId);
            if (createContext == null)
            {
                return HttpNotFound();
            }

            ApplyCreateViewBag(createContext);
            return View(createContext.InitialModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(DepartmentCreateVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            int? contextParentId = null;
            if (model != null && model.LockedParentDepartmentId.HasValue)
            {
                contextParentId = model.LockedParentDepartmentId;
            }
            else if (model != null && model.ParentDepartmentId.HasValue)
            {
                contextParentId = model.ParentDepartmentId;
            }

            var createContext = ResolveCreateContext(model == null ? null : model.SetupMode, contextParentId);
            if (createContext == null)
            {
                return HttpNotFound();
            }

            ApplyCreateViewBag(createContext);

            if (model == null)
            {
                ModelState.AddModelError("", "Department details are required.");
                return View(createContext.InitialModel);
            }

            if (createContext.LockedParentDepartmentId.HasValue)
            {
                model.LockedParentDepartmentId = createContext.LockedParentDepartmentId;
                model.ParentDepartmentId = createContext.LockedParentDepartmentId;
                model.SetupMode = createContext.SetupMode;
            }
            else if (!createContext.ShowSetupModePicker)
            {
                model.SetupMode = createContext.SetupMode;
            }

            try
            {
                var departmentId = _departmentService.CreateFromWizard(model);
                TempData["Message"] = DepartmentCreateUserMessages.GetCreateSuccessMessage(model.SetupMode);
                TempData["Guidance"] = DepartmentCreateUserMessages.GetCreateGuidance(model.SetupMode);
                return RedirectToAction("Details", new { id = departmentId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            ApplyEditViewBag(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(DepartmentVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            if (!ModelState.IsValid)
            {
                ApplyEditViewBag(model);
                return View(model);
            }

            try
            {
                _departmentService.Update(model);
                TempData["Message"] = "Department updated.";
                return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ApplyEditViewBag(model);
                return View(model);
            }
        }

        private sealed class DepartmentCreateContext
        {
            public string SetupMode { get; set; }
            public int? LockedParentDepartmentId { get; set; }
            public string LockedParentName { get; set; }
            public bool ShowSetupModePicker { get; set; }
            public DepartmentCreateVm InitialModel { get; set; }
        }

        private DepartmentCreateContext ResolveCreateContext(string setupMode, int? parentId)
        {
            var normalizedMode = string.IsNullOrWhiteSpace(setupMode)
                ? null
                : setupMode.Trim();

            if (parentId.HasValue && parentId.Value > 0)
            {
                var parent = _departmentService.GetById(parentId.Value);
                if (parent == null || !parent.IsActive)
                {
                    return null;
                }

                var parentEntity = MapToDepartment(parent);
                if (string.IsNullOrWhiteSpace(normalizedMode))
                {
                    if (DepartmentHierarchyRules.CanCreateRoomUnderSubDepartment(parentEntity))
                    {
                        normalizedMode = DepartmentService.SetupModeRoom;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (string.Equals(normalizedMode, DepartmentService.SetupModeSubDepartment, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedMode = DepartmentService.SetupModeSubDepartment;
                }
                else if (string.Equals(normalizedMode, DepartmentService.SetupModeRoom, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedMode = DepartmentService.SetupModeRoom;
                }
                else
                {
                    return null;
                }

                return BuildLockedContext(normalizedMode, parent);
            }

            if (string.Equals(normalizedMode, DepartmentService.SetupModeNormal, StringComparison.OrdinalIgnoreCase))
            {
                return new DepartmentCreateContext
                {
                    SetupMode = DepartmentService.SetupModeNormal,
                    ShowSetupModePicker = false,
                    InitialModel = new DepartmentCreateVm { SetupMode = DepartmentService.SetupModeNormal }
                };
            }

            if (string.Equals(normalizedMode, DepartmentService.SetupModeRoom, StringComparison.OrdinalIgnoreCase))
            {
                return new DepartmentCreateContext
                {
                    SetupMode = DepartmentService.SetupModeRoom,
                    ShowSetupModePicker = false,
                    InitialModel = new DepartmentCreateVm
                    {
                        SetupMode = DepartmentService.SetupModeRoom,
                        IsRequisitionTarget = true
                    }
                };
            }

            if (!string.IsNullOrWhiteSpace(normalizedMode)
                && !string.Equals(normalizedMode, DepartmentService.SetupModeGradeStreams, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedMode, DepartmentService.SetupModeBulkGrades, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var defaultMode = string.IsNullOrWhiteSpace(normalizedMode)
                ? DepartmentService.SetupModeNormal
                : normalizedMode;
            return new DepartmentCreateContext
            {
                SetupMode = defaultMode,
                ShowSetupModePicker = true,
                InitialModel = new DepartmentCreateVm { SetupMode = defaultMode }
            };
        }

        private DepartmentCreateContext BuildLockedContext(string setupMode, DepartmentVm parent)
        {
            if (!DepartmentHierarchyRules.IsValidLockedParentForSetupMode(setupMode, MapToDepartment(parent)))
            {
                return null;
            }

            return new DepartmentCreateContext
            {
                SetupMode = setupMode,
                LockedParentDepartmentId = parent.Id,
                LockedParentName = DepartmentLabelHelper.FormatCodeName(parent.Code, parent.Name),
                ShowSetupModePicker = false,
                InitialModel = new DepartmentCreateVm
                {
                    SetupMode = setupMode,
                    ParentDepartmentId = parent.Id,
                    LockedParentDepartmentId = parent.Id,
                    IsRequisitionTarget = setupMode == DepartmentService.SetupModeRoom
                }
            };
        }

        private void ApplyCreateViewBag(DepartmentCreateContext context)
        {
            ViewBag.SetupModes = BuildSetupModeSelectList(context.SetupMode, context.ShowSetupModePicker);
            ViewBag.AdminParentDepartments = BuildAdminParentDepartmentSelectList(context.InitialModel.ParentDepartmentId);
            ViewBag.ShowSetupModePicker = context.ShowSetupModePicker;
            ViewBag.LockedParentDepartmentId = context.LockedParentDepartmentId;
            ViewBag.LockedParentName = context.LockedParentName;
            ViewBag.CreateSetupMode = context.SetupMode;
            if (context.LockedParentDepartmentId.HasValue)
            {
                var lockedParent = _departmentService.GetById(context.LockedParentDepartmentId.Value);
                ViewBag.LockedParentDepartmentKind = lockedParent == null
                    ? (DepartmentKind?)null
                    : lockedParent.DepartmentKind;
            }
        }

        private static Domain.Entities.Department MapToDepartment(DepartmentVm model)
        {
            return new Domain.Entities.Department
            {
                Id = model.Id,
                Name = model.Name,
                Code = model.Code,
                IsActive = model.IsActive,
                DepartmentKind = model.DepartmentKind,
                ParentDepartmentId = model.ParentDepartmentId
            };
        }

        private void ApplyEditViewBag(DepartmentVm model)
        {
            if (model == null)
            {
                return;
            }

            var scopedDepartments = DepartmentListHelper.DeduplicateById(_departmentService.GetAll());
            EnsureParentDepartmentName(model, scopedDepartments);

            if (model.DepartmentKind == DepartmentKind.Room)
            {
                ViewBag.RoomParentCandidates = DepartmentRoomParentCandidates.GetRoomParentCandidates(model, scopedDepartments);
                ViewBag.RoomOtherParentGroups = DepartmentRoomParentCandidates.BuildOtherParentPickerGroups(scopedDepartments);
                ViewBag.RoomParentOtherValue = DepartmentLabelHelper.RoomParentOtherOptionValue;
            }
            else if (model.DepartmentKind == DepartmentKind.SubDepartment)
            {
                ViewBag.SubDepartmentParentSelectList = BuildTopLevelAdminParentSelectList(
                    scopedDepartments,
                    model.ParentDepartmentId);
            }
        }

        private static void EnsureParentDepartmentName(DepartmentVm model, IList<DepartmentVm> scopedDepartments)
        {
            if (!model.ParentDepartmentId.HasValue || !string.IsNullOrWhiteSpace(model.ParentDepartmentName))
            {
                return;
            }

            var parent = scopedDepartments.FirstOrDefault(x => x.Id == model.ParentDepartmentId.Value);
            if (parent != null)
            {
                model.ParentDepartmentName = DepartmentLabelHelper.FormatCodeName(parent.Code, parent.Name);
            }
        }

        private static SelectList BuildTopLevelAdminParentSelectList(
            IList<DepartmentVm> scopedDepartments,
            int? selectedParentDepartmentId)
        {
            var items = DepartmentRoomParentCandidates.GetTopLevelAdministrativeParents(scopedDepartments)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = DepartmentLabelHelper.FormatCodeName(x.Code, x.Name)
                })
                .ToList();
            return new SelectList(items, "Value", "Text", selectedParentDepartmentId);
        }

        private static SelectList BuildSetupModeSelectList(string selected, bool includeOrgModes)
        {
            var items = new System.Collections.Generic.List<object>
            {
                new { Value = DepartmentService.SetupModeNormal, Text = "Administrative department" }
            };

            if (includeOrgModes)
            {
                items.Add(new { Value = DepartmentService.SetupModeGradeStreams, Text = "Grade with class streams" });
                items.Add(new { Value = DepartmentService.SetupModeBulkGrades, Text = "Bulk grades 1–6" });
            }

            return new SelectList(items, "Value", "Text", selected);
        }

        private SelectList BuildAdminParentDepartmentSelectList(int? selectedParentDepartmentId)
        {
            var parents = _departmentService.GetAll()
                .Where(x => x.IsActive
                    && x.DepartmentKind == DepartmentKind.Administrative
                    && !x.ParentDepartmentId.HasValue)
                .OrderBy(x => x.Name)
                .ToList();
            var items = parents
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = DepartmentLabelHelper.FormatCodeName(x.Code, x.Name)
                })
                .ToList();
            return new SelectList(items, "Value", "Text", selectedParentDepartmentId);
        }
    }
}
