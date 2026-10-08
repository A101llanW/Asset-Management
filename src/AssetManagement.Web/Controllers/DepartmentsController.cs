using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
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
            EnrichDepartmentPicNames(itemsList);
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

        /// <summary>
        /// Requisition flows overview: every flow-configurable unit (Department / Sub / Room / Class) with its mode,
        /// effective flow and PO raiser. Renders in place (no redirect to Index); Edit returns to the unit.
        /// </summary>
        public ActionResult RequisitionFlows(string search = null, string status = null, string view = null)
        {
            var all = _departmentService.GetAll().ToList();
            var names = all.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
            IEnumerable<DepartmentVm> items = all.Where(x => DepartmentHierarchyRules.ShowsRequisitionFlowColumns(x.DepartmentKind));
            foreach (var item in items)
            {
                string parentName;
                if (string.IsNullOrWhiteSpace(item.ParentDepartmentName)
                    && item.ParentDepartmentId.HasValue
                    && names.TryGetValue(item.ParentDepartmentId.Value, out parentName))
                {
                    item.ParentDepartmentName = parentName;
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x =>
                    (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Code ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.ParentDepartmentName ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.EffectiveRequisitionFlowSummary ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.EffectivePurchaseOrderRaiserSummary ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            switch ((status ?? "active").ToLowerInvariant())
            {
                case "all":
                    status = "all";
                    break;
                case "inactive":
                    status = "inactive";
                    items = items.Where(x => !x.IsActive);
                    break;
                default:
                    status = "active";
                    items = items.Where(x => x.IsActive);
                    break;
            }

            ViewBag.StatusFilter = status;
            ViewBag.Search = search;
            return View(items
                .OrderBy(x => DepartmentOrgHierarchyDisplay.FormatDepartmentKind(x.DepartmentKind))
                .ThenBy(x => x.Name)
                .ToList());
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (DepartmentHierarchyRules.ShowsRequisitionFlowColumns(model.DepartmentKind))
            {
                ViewBag.RequisitionFlowApprovalSummary = BuildDepartmentRequisitionApprovalSummary(model.Id);
            }

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
        public ActionResult Create(string setupMode = null, string kind = null, int? parentId = null, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            setupMode = ResolveSetupModeAlias(setupMode, kind);
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
            else if (!createContext.ShowSetupModePicker
                || string.IsNullOrWhiteSpace(model.SetupMode))
            {
                // Prefer resolved context (includes kind= alias) when binder left SetupMode empty.
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

            // Back/Cancel and Save both return to this unit (Details), carrying the original list URL onward.
            ViewBag.ReturnUrl = BuildUnitReturnUrl(id, returnUrl);
            ApplyEditViewBag(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(DepartmentVm model, string returnUrl = null)
        {
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = BuildUnitReturnUrl(model.Id, returnUrl);
            var flowConfigurable = DepartmentHierarchyRules.CanConfigureRequisitionFlow(model.DepartmentKind);
            if (flowConfigurable)
            {
                // Model-level errors so they show in the form summary and the page re-renders in place.
                DepartmentRequisitionApprovalSettingsHelper.ValidateCustomApproval(
                    model,
                    (key, message) => ModelState.AddModelError(string.Empty, message));
                if (model.UseCustomRequisitionApproval)
                {
                    ValidateStagePeopleAndPurchaseOrderRaiser(model);
                }
            }

            if (!ModelState.IsValid)
            {
                ApplyEditViewBag(model);
                return View(model);
            }

            try
            {
                _departmentService.Update(model);
                TempData["Message"] = flowConfigurable
                    ? (model.DepartmentKind == DepartmentKind.Administrative
                        ? "Department"
                        : DepartmentOrgHierarchyDisplay.FormatDepartmentKind(model.DepartmentKind)) + " updated. Requisition flow saved."
                    : "Department updated.";
                return Redirect((string)ViewBag.ReturnUrl);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                ApplyEditViewBag(model);
                return View(model);
            }
        }

        /// <summary>
        /// Edit helper (AJAX): active children that the proposed Kind/Parent would make invalid, with valid
        /// new-parent candidates built from live org-scoped data. Server re-validates on POST.
        /// </summary>
        [HttpGet]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult KindChangeImpact(int id, int kind, int? parentId = null)
        {
            if (!Enum.IsDefined(typeof(DepartmentKind), kind))
            {
                return Json(new { ok = false, message = "Unknown department kind." }, JsonRequestBehavior.AllowGet);
            }

            var current = _departmentService.GetById(id);
            if (current == null)
            {
                return HttpNotFound();
            }

            var impact = _departmentService.GetKindChangeImpact(
                id,
                (DepartmentKind)kind,
                parentId.HasValue && parentId.Value > 0 ? parentId : null);
            return Json(new
            {
                ok = true,
                departmentId = impact.DepartmentId,
                newKindLabel = impact.NewKindLabel,
                items = impact.Items.Select(x => new
                {
                    childId = x.ChildId,
                    childName = x.ChildName,
                    childCode = x.ChildCode,
                    childKindLabel = x.ChildKindLabel,
                    reason = x.Reason,
                    candidates = x.Candidates.Select(c => new { id = c.Id, label = c.Label }).ToList()
                }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        private sealed class DepartmentCreateContext
        {
            public string SetupMode { get; set; }
            public int? LockedParentDepartmentId { get; set; }
            public string LockedParentName { get; set; }
            public bool ShowSetupModePicker { get; set; }
            public DepartmentCreateVm InitialModel { get; set; }
        }


        /// <summary>
        /// QA and deep-links may pass kind=Room|SubDepartment; map to SetupMode values.
        /// </summary>
        private static string ResolveSetupModeAlias(string setupMode, string kind)
        {
            if (!string.IsNullOrWhiteSpace(setupMode))
            {
                return setupMode.Trim();
            }

            if (string.IsNullOrWhiteSpace(kind))
            {
                return setupMode;
            }

            switch (kind.Trim())
            {
                case "Room":
                    return DepartmentService.SetupModeRoom;
                case "SubDepartment":
                    return DepartmentService.SetupModeSubDepartment;
                case "Administrative":
                case "Normal":
                    return DepartmentService.SetupModeNormal;
                case "GradeWithStreams":
                case "Grade":
                    return DepartmentService.SetupModeGradeStreams;
                case "BulkGrades":
                    return DepartmentService.SetupModeBulkGrades;
                default:
                    return kind.Trim();
            }
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
            ViewBag.RoomCustodianUsers = BuildActiveUserSelectList(context.InitialModel == null ? null : context.InitialModel.RoomCustodianUserId);
            ViewBag.ClassTeacherUsers = BuildActiveUserSelectList(context.InitialModel == null ? null : context.InitialModel.ClassTeacherUserId);
            if (context.LockedParentDepartmentId.HasValue)
            {
                var lockedParent = _departmentService.GetById(context.LockedParentDepartmentId.Value);
                ViewBag.LockedParentDepartmentKind = lockedParent == null
                    ? (DepartmentKind?)null
                    : lockedParent.DepartmentKind;
            }
        }


        private void EnrichDepartmentPicNames(IList<DepartmentVm> departments)
        {
            if (departments == null || departments.Count == 0)
            {
                return;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dept in departments)
            {
                CollectPicIds(dept, ids);
            }

            if (ids.Count == 0)
            {
                return;
            }

            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                var user = BuildUserService().GetById(id);
                if (user != null)
                {
                    labels[id] = BuildUserLabel(user);
                }
            }

            foreach (var dept in departments)
            {
                ApplyPicLabels(dept, labels);
            }
        }

        private static void CollectPicIds(DepartmentVm dept, ISet<string> ids)
        {
            if (dept == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dept.ClassTeacherUserId))
            {
                ids.Add(dept.ClassTeacherUserId.Trim());
            }

            if (!string.IsNullOrWhiteSpace(dept.RoomCustodianUserId))
            {
                ids.Add(dept.RoomCustodianUserId.Trim());
            }

            if (dept.Children == null)
            {
                return;
            }

            foreach (var child in dept.Children)
            {
                CollectPicIds(child, ids);
            }
        }

        private static void ApplyPicLabels(DepartmentVm dept, IDictionary<string, string> labels)
        {
            if (dept == null)
            {
                return;
            }

            string label;
            if (!string.IsNullOrWhiteSpace(dept.ClassTeacherUserId)
                && labels.TryGetValue(dept.ClassTeacherUserId.Trim(), out label))
            {
                dept.ClassTeacherName = label;
            }

            if (!string.IsNullOrWhiteSpace(dept.RoomCustodianUserId)
                && labels.TryGetValue(dept.RoomCustodianUserId.Trim(), out label))
            {
                dept.RoomCustodianName = label;
            }

            if (dept.Children == null)
            {
                return;
            }

            foreach (var child in dept.Children)
            {
                ApplyPicLabels(child, labels);
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

            // Always populate parent pickers so Kind changes (e.g. Grade->Room) can show Parent in the UI.
            ViewBag.RoomParentCandidates = DepartmentRoomParentCandidates.GetRoomParentCandidates(model, scopedDepartments);
            ViewBag.RoomOtherParentGroups = DepartmentRoomParentCandidates.BuildOtherParentPickerGroups(scopedDepartments, model.Id);
            ViewBag.RoomParentOtherValue = DepartmentLabelHelper.RoomParentOtherOptionValue;
            ViewBag.SubDepartmentParentSelectList = BuildTopLevelAdminParentSelectList(
                scopedDepartments,
                model.ParentDepartmentId,
                model.Id);
            ViewBag.ClassGradeParentSelectList = BuildGradeParentSelectList(
                scopedDepartments,
                model,
                model.ParentDepartmentId);
            ViewBag.DepartmentKindSelectList = BuildDepartmentKindSelectList(model.DepartmentKind);
            var stored = model.Id > 0 ? _departmentService.GetById(model.Id) : null;
            ViewBag.OriginalDepartmentKind = DepartmentKindSelectListBuilder.ToOptionValue(
                stored != null ? stored.DepartmentKind : model.DepartmentKind);
            ViewBag.OriginalParentDepartmentId = stored != null && stored.ParentDepartmentId.HasValue
                ? stored.ParentDepartmentId.Value.ToString()
                : string.Empty;
            ViewBag.RequisitionInheritOptions = model.Id > 0
                ? _departmentService.GetRequisitionInheritOptions(model.Id)
                : new List<RequisitionInheritOptionVm>();
            ViewBag.EffectivePurchaseOrderRaiserSummary = stored != null ? stored.EffectivePurchaseOrderRaiserSummary : null;
            ViewBag.EffectiveRequisitionFlowSummary = stored != null ? stored.EffectiveRequisitionFlowSummary : null;
            // Live summary on Edit: what each "Inherit flow from" choice resolves to (client switches between them).
            ViewBag.RequisitionInheritPreviewsJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                model.Id > 0 && DepartmentHierarchyRules.CanConfigureRequisitionFlow(model.DepartmentKind)
                    ? _departmentService.GetRequisitionInheritPreviews(model.Id)
                    : new List<RequisitionInheritPreviewVm>());
            ViewBag.ClassTeacherUsers = BuildActiveUserSelectList(model.ClassTeacherUserId);
            ViewBag.RoomCustodianUsers = BuildActiveUserSelectList(model.RoomCustodianUserId);

            if (DepartmentHierarchyRules.CanConfigureRequisitionFlow(model.DepartmentKind))
            {
                PopulateRoomRequisitionApproval(model);
            }
        }

        private static SelectList BuildDepartmentKindSelectList(DepartmentKind selectedKind)
        {
            // Option Values are int strings ("4"=Room). Pass selectedValue as the same string type.
            // Edit.cshtml must not use DropDownListFor here: it stringifies the enum as "Room" and
            // fails to match "4", so Administrative (first option) incorrectly appears selected.
            var items = DepartmentKindSelectListBuilder.BuildItems(selectedKind)
                .Select(x => new SelectListItem
                {
                    Value = x.Value,
                    Text = x.Text,
                    Selected = x.Selected
                })
                .ToList();
            return new SelectList(items, "Value", "Text", DepartmentKindSelectListBuilder.ToOptionValue(selectedKind));
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


        private static SelectList BuildGradeParentSelectList(
            IList<DepartmentVm> scopedDepartments,
            DepartmentVm model,
            int? selectedParentDepartmentId)
        {
            var items = DepartmentRoomParentCandidates.GetGradeParentsForClass(model, scopedDepartments)
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
        private static SelectList BuildTopLevelAdminParentSelectList(
            IList<DepartmentVm> scopedDepartments,
            int? selectedParentDepartmentId,
            int? excludeSubtreeRootId = null)
        {
            var excluded = excludeSubtreeRootId.HasValue && excludeSubtreeRootId.Value > 0
                ? DepartmentSubtreeHelper.GetSelfAndDescendantIds(excludeSubtreeRootId.Value, scopedDepartments)
                : new HashSet<int>();
            var parents = DepartmentRoomParentCandidates.GetTopLevelAdministrativeParents(scopedDepartments, excludeSubtreeRootId)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();
            if (selectedParentDepartmentId.HasValue
                && !excluded.Contains(selectedParentDepartmentId.Value)
                && !parents.Any(x => x.Id == selectedParentDepartmentId.Value))
            {
                var current = (scopedDepartments ?? Enumerable.Empty<DepartmentVm>())
                    .FirstOrDefault(x => x.Id == selectedParentDepartmentId.Value);
                if (current != null)
                {
                    parents.Insert(0, current);
                }
            }

            var items = parents
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = DepartmentLabelHelper.FormatCodeName(x.Code, x.Name)
                })
                .ToList();
            return new SelectList(items, "Value", "Text", selectedParentDepartmentId);
        }

        private void PopulateRoomRequisitionApproval(DepartmentVm model)
        {
            if (model == null)
            {
                return;
            }

            var entity = UnitOfWork.Repository<Department>().GetById(model.Id);
            // On a POST re-render (validation error) keep the posted Inherit/Custom choice; only GET loads the stored mode.
            var isPostBack = HttpContext != null
                && HttpContext.Request != null
                && string.Equals(HttpContext.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
            if (entity != null && !isPostBack)
            {
                model.UseCustomRequisitionApproval = entity.UseCustomRequisitionApproval;
            }

            if (model.RequisitionApprovalProcesses == null || model.RequisitionApprovalProcesses.Count == 0)
            {
                model.RequisitionApprovalProcesses = DepartmentRequisitionApprovalSettingsHelper
                    .BuildForDepartment(entity, UnitOfWork, GetRolesForOrganization())
                    .ToList();
            }

            ViewBag.RoleOptions = BuildRoleOptionList();
            PopulateStagePersonOptions();
        }

        /// <summary>
        /// Return target for Edit (Back/Cancel and after Save): this unit's Details page. A safe local returnUrl
        /// (tenant-slug and virtual-path aware via FormatSafeReturnPath) is kept as the Details page's own Back link,
        /// so the user stays on the unit instead of bouncing to the list.
        /// </summary>
        private string BuildUnitReturnUrl(int id, string returnUrl)
        {
            var unitDetails = Url.Action("Details", new { id });
            var safe = FormatSafeReturnPath(returnUrl);
            if (string.IsNullOrWhiteSpace(safe) || string.IsNullOrWhiteSpace(unitDetails))
            {
                return unitDetails;
            }

            var safePath = safe.Split('?')[0].TrimEnd('/');
            var detailsPath = unitDetails.Split('?')[0].TrimEnd('/');
            if (string.Equals(safePath, detailsPath, StringComparison.OrdinalIgnoreCase))
            {
                return safe;
            }

            var editPath = (Url.Action("Edit", new { id }) ?? string.Empty).Split('?')[0].TrimEnd('/');
            if (string.Equals(safePath, editPath, StringComparison.OrdinalIgnoreCase))
            {
                return unitDetails;
            }

            return Url.Action("Details", new { id, returnUrl = safe });
        }

        /// <summary>
        /// Server validation for the optional per-stage approver person and the PO raiser: user must be active,
        /// in this org and hold the role; role must exist in this org. A person whose role changed is cleared.
        /// Stored values come from the DB (never trusted from the post); users/roles are current-org only.
        /// </summary>
        private void ValidateStagePeopleAndPurchaseOrderRaiser(DepartmentVm model)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return;
            }

            var stored = UnitOfWork.Repository<Department>().GetById(model.Id);
            if (stored == null || (stored.OrganizationId.HasValue && stored.OrganizationId.Value != organizationId.Value))
            {
                ModelState.AddModelError(string.Empty, "Department not found in this organisation.");
                return;
            }

            var storedIsCustom = stored.UseCustomRequisitionApproval || stored.RequisitionFlowMode == RequisitionFlowMode.Custom;
            var storedRoleIds = storedIsCustom
                ? ApprovalWorkflowSettingsHelper.ParseStageRoleIds(
                    !string.IsNullOrWhiteSpace(stored.CustomStageRoleIds) ? stored.CustomStageRoleIds : stored.RequisitionApprovalStageRoleIds)
                : new List<int>();
            var roleIds = GetOrganizationRoleIdSet(organizationId.Value);
            var findUser = BuildOrgApproverUserFinder(organizationId.Value);
            Action<string, string> addError = (key, message) => ModelState.AddModelError(string.Empty, message);

            var processes = model.RequisitionApprovalProcesses ?? new List<ApprovalProcessSettingsVm>();
            foreach (var process in processes.Where(x => x != null))
            {
                StageApproverRules.ValidateStages(
                    process.Stages,
                    storedRoleIds,
                    organizationId.Value,
                    roleIds,
                    findUser,
                    addError,
                    "RequisitionApprovalProcesses.Stages");
            }

            model.PurchaseOrderRaiserUserId = StageApproverRules.ValidatePurchaseOrderRaiser(
                model.PurchaseOrderRaiserRoleId,
                model.PurchaseOrderRaiserUserId,
                storedIsCustom ? stored.PurchaseOrderRaiserRoleId : null,
                organizationId.Value,
                roleIds,
                findUser,
                addError);
            if (model.PurchaseOrderRaiserRoleId.HasValue && model.PurchaseOrderRaiserRoleId.Value <= 0)
            {
                model.PurchaseOrderRaiserRoleId = null;
            }

            // Validated values replace the posted ones in ModelState so a re-render shows the normalized choice.
            ModelState.Remove("PurchaseOrderRaiserUserId");
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
                items.Add(new { Value = DepartmentService.SetupModeBulkGrades, Text = "Bulk grades 1-6" });
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
