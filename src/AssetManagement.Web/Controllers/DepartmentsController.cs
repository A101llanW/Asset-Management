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

        public ActionResult Index(
            string search = null,
            string status = "active",
            string view = "tree",
            string domain = null,
            string[] kind = null)
        {
            domain = DepartmentHierarchyRules.NormalizeDomain(domain);
            var selectedKinds = ParseKindFilters(kind);

            var items = FilterBySearch(_departmentService.GetAll(), search, (x, term) =>
                (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Code ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term));

            items = items.Where(x => DepartmentHierarchyRules.BelongsToDomain(x.DepartmentKind, domain));

            if (selectedKinds.Count > 0)
            {
                items = items.Where(x => selectedKinds.Contains(x.DepartmentKind));
            }

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

            var itemList = items.ToList();

            ViewBag.StatusFilter = status;
            ViewBag.ViewMode = string.Equals(view, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "tree";
            ViewBag.Search = search;
            ViewBag.Domain = domain;
            ViewBag.SelectedKinds = selectedKinds.Select(x => x.ToString()).ToList();
            ViewBag.AvailableKinds = domain == DepartmentHierarchyRules.DomainClasses
                ? new[] { DepartmentKind.Grade, DepartmentKind.Class }
                : new[] { DepartmentKind.Administrative, DepartmentKind.SubDepartment, DepartmentKind.Room };
            ViewBag.TreeSections = _departmentService.GetTreeSections(domain)
                .Select(section => new DepartmentTreeSectionVm
                {
                    Title = section.Title,
                    Items = section.Items
                        .Where(item => itemList.Any(x => x.Id == item.Id || item.Children.Any(child => child.Id == x.Id)))
                        .Select(item =>
                        {
                            if (selectedKinds.Count == 0)
                            {
                                return item;
                            }

                            var filteredChildren = item.Children
                                .Where(child => itemList.Any(x => x.Id == child.Id))
                                .ToList();
                            return new DepartmentVm
                            {
                                Id = item.Id,
                                Name = item.Name,
                                Code = item.Code,
                                Description = item.Description,
                                ParentDepartmentId = item.ParentDepartmentId,
                                ParentDepartmentName = item.ParentDepartmentName,
                                DepartmentKind = item.DepartmentKind,
                                IsRequisitionTarget = item.IsRequisitionTarget,
                                RequisitionFlowMode = item.RequisitionFlowMode,
                                IsActive = item.IsActive,
                                Children = filteredChildren
                            };
                        })
                        .Where(item => itemList.Any(x => x.Id == item.Id) || item.Children.Any())
                        .ToList()
                })
                .Where(section => section.Items.Any())
                .ToList();

            return View(itemList.OrderBy(x => x.DepartmentKind).ThenBy(x => x.Name).ToList());
        }

        /// <summary>
        /// Admin list: every org requisition leaf (Room + SubDept/Admin IsRequisitionTarget) with current flow setup. Edit opens Departments/Edit; Save returns here via returnUrl.
        /// </summary>
        public ActionResult RequisitionFlows(string search = null, string status = "active")
        {
            var items = _departmentService.GetRoomRequisitionFlows();

            items = FilterBySearch(items, search, (x, term) =>
                (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Code ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.ParentDepartmentName ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.EffectiveRequisitionFlowSummary ?? string.Empty).ToLowerInvariant().Contains(term));

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
            ViewBag.Search = search;
            return View(items.ToList());
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index", null, new
            {
                domain = DepartmentHierarchyRules.IsAcademic(model.DepartmentKind)
                    ? DepartmentHierarchyRules.DomainClasses
                    : DepartmentHierarchyRules.DomainOrg
            });
            var organizationId = ResolveCurrentOrganizationId();
            ViewBag.ActiveUserCount = organizationId.HasValue
                ? DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Queries.IUserAccountQueryRepository>()
                    .CountActiveUsersForDepartment(organizationId.Value, id)
                : 0;
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { DepartmentId = model.Id });
            ViewBag.Domain = DepartmentHierarchyRules.IsAcademic(model.DepartmentKind)
                ? DepartmentHierarchyRules.DomainClasses
                : DepartmentHierarchyRules.DomainOrg;
            return View(model);
        }

        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(string returnUrl = null, string setupMode = null, string domain = null)
        {
            domain = ResolveCreateDomain(domain, setupMode);
            var mode = ResolveSetupMode(setupMode, domain);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index", null, new { domain });
            ViewBag.Domain = domain;
            ViewBag.SetupModeLocked = !string.IsNullOrWhiteSpace(setupMode);
            ViewBag.SetupModes = BuildSetupModeSelectList(mode, domain);
            ViewBag.AdminParentDepartments = BuildAdminParentDepartmentSelectList(null);
            ViewBag.OrganizationalParentDepartments = BuildOrganizationalParentSelectList(0, null);
            ViewBag.CreateTitle = GetCreateTitle(mode);
            return View(new DepartmentCreateVm { SetupMode = mode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(DepartmentCreateVm model, string returnUrl = null, string domain = null)
        {
            var mode = model == null ? DepartmentService.SetupModeNormal : (model.SetupMode ?? DepartmentService.SetupModeNormal);
            domain = ResolveCreateDomain(domain, mode);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index", null, new { domain });
            ViewBag.Domain = domain;
            ViewBag.SetupModeLocked = false;
            ViewBag.SetupModes = BuildSetupModeSelectList(mode, domain);
            ViewBag.AdminParentDepartments = BuildAdminParentDepartmentSelectList(model == null ? null : model.ParentDepartmentId);
            ViewBag.OrganizationalParentDepartments = BuildOrganizationalParentSelectList(0, model == null ? null : model.ParentDepartmentId);
            ViewBag.CreateTitle = GetCreateTitle(mode);
            if (model == null)
            {
                ModelState.AddModelError("", "Department details are required.");
                return View(new DepartmentCreateVm { SetupMode = DepartmentService.SetupModeNormal });
            }

            try
            {
                var departmentId = _departmentService.CreateFromWizard(model);
                TempData["Message"] = GetCreateSuccessMessage(mode);
                TempData["Guidance"] = GetCreateGuidance(mode);
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
            ViewBag.Domain = DepartmentHierarchyRules.IsAcademic(model.DepartmentKind)
                ? DepartmentHierarchyRules.DomainClasses
                : DepartmentHierarchyRules.DomainOrg;
            if (!DepartmentHierarchyRules.IsAcademic(model.DepartmentKind))
            {
                ViewBag.ParentDepartments = BuildOrganizationalParentSelectList(model.Id, model.ParentDepartmentId);
                ViewBag.RoleOptions = BuildRoleOptionList();
                PopulateInheritedFlowPreview(model.Id, model.ParentDepartmentId, model.RequisitionFlowMode);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(DepartmentVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            ViewBag.Domain = DepartmentHierarchyRules.IsAcademic(model.DepartmentKind)
                ? DepartmentHierarchyRules.DomainClasses
                : DepartmentHierarchyRules.DomainOrg;
            if (!DepartmentHierarchyRules.IsAcademic(model.DepartmentKind))
            {
                ViewBag.ParentDepartments = BuildOrganizationalParentSelectList(model.Id, model.ParentDepartmentId);
                ViewBag.RoleOptions = BuildRoleOptionList();
                PopulateInheritedFlowPreview(model.Id, model.ParentDepartmentId, model.RequisitionFlowMode);
            }
            if (model != null
                && !DepartmentHierarchyRules.IsAcademic(model.DepartmentKind)
                && model.CustomStages != null)
            {
                var serializedRoles = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(
                    model.CustomStages.Select(x => x.RoleId));
                var serializedUsers = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(
                    model.CustomStages.Select(x => x.UserId));
                var hasConfiguredStages = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(serializedRoles).Count > 0;

                // Inherit with empty hierarchy: Admin may set stages on this Edit screen -> store as Custom.
                if (model.RequisitionFlowMode == RequisitionFlowMode.Custom
                    || (model.RequisitionFlowMode == RequisitionFlowMode.InheritParent && hasConfiguredStages))
                {
                    if (hasConfiguredStages && model.RequisitionFlowMode == RequisitionFlowMode.InheritParent)
                    {
                        model.RequisitionFlowMode = RequisitionFlowMode.Custom;
                    }

                    if (model.RequisitionFlowMode == RequisitionFlowMode.Custom)
                    {
                        model.CustomStageRoleIds = serializedRoles;
                        model.CustomStageUserIds = serializedUsers;
                    }
                }
            }
            if (!ModelState.IsValid)
            {
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
                return View(model);
            }
        }

        private static IList<DepartmentKind> ParseKindFilters(string[] kind)
        {
            var result = new List<DepartmentKind>();
            if (kind == null || kind.Length == 0)
            {
                return result;
            }

            foreach (var raw in kind)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                DepartmentKind parsed;
                if (Enum.TryParse(raw.Trim(), true, out parsed) && !result.Contains(parsed))
                {
                    result.Add(parsed);
                }
            }

            return result;
        }

        private static string ResolveCreateDomain(string domain, string setupMode)
        {
            if (!string.IsNullOrWhiteSpace(domain))
            {
                return DepartmentHierarchyRules.NormalizeDomain(domain);
            }

            if (string.Equals(setupMode, DepartmentService.SetupModeGradeStreams, StringComparison.OrdinalIgnoreCase)
                || string.Equals(setupMode, DepartmentService.SetupModeBulkGrades, StringComparison.OrdinalIgnoreCase))
            {
                return DepartmentHierarchyRules.DomainClasses;
            }

            return DepartmentHierarchyRules.DomainOrg;
        }

        private static string ResolveSetupMode(string setupMode, string domain)
        {
            if (!string.IsNullOrWhiteSpace(setupMode))
            {
                return setupMode.Trim();
            }

            return domain == DepartmentHierarchyRules.DomainClasses
                ? DepartmentService.SetupModeGradeStreams
                : DepartmentService.SetupModeNormal;
        }

        private static string GetCreateTitle(string setupMode)
        {
            switch (setupMode)
            {
                case DepartmentService.SetupModeSubDepartment:
                    return "Add sub-unit";
                case DepartmentService.SetupModeRoom:
                    return "Add room";
                case DepartmentService.SetupModeGradeStreams:
                    return "Add grade & streams";
                case DepartmentService.SetupModeBulkGrades:
                    return "Bulk create grades";
                default:
                    return "Add department";
            }
        }

        private static string GetCreateSuccessMessage(string setupMode)
        {
            switch (setupMode)
            {
                case DepartmentService.SetupModeSubDepartment:
                    return "Sub-unit created.";
                case DepartmentService.SetupModeRoom:
                    return "Room created.";
                case DepartmentService.SetupModeGradeStreams:
                    return "Grade and streams created.";
                case DepartmentService.SetupModeBulkGrades:
                    return "Grades and streams created.";
                default:
                    return "Department created.";
            }
        }

        private static string GetCreateGuidance(string setupMode)
        {
            switch (setupMode)
            {
                case DepartmentService.SetupModeSubDepartment:
                    return "Next step: assign users or assets to this sub-unit.";
                case DepartmentService.SetupModeRoom:
                    return "Next step: assign assets to this room or adjust its requisition flow.";
                case DepartmentService.SetupModeGradeStreams:
                case DepartmentService.SetupModeBulkGrades:
                    return "Next step: review grades and streams, then relocate or assign classroom assets.";
                default:
                    return "Next step: review the department details and then add users or assign assets to this department.";
            }
        }

        private static SelectList BuildSetupModeSelectList(string selected, string domain)
        {
            IEnumerable<object> items;
            if (domain == DepartmentHierarchyRules.DomainClasses)
            {
                items = new[]
                {
                    new { Value = DepartmentService.SetupModeGradeStreams, Text = "Grade with streams" },
                    new { Value = DepartmentService.SetupModeBulkGrades, Text = "Bulk grade range" }
                };
            }
            else
            {
                items = new[]
                {
                    new { Value = DepartmentService.SetupModeNormal, Text = "Normal (administrative)" },
                    new { Value = DepartmentService.SetupModeSubDepartment, Text = "Sub-unit under admin department" },
                    new { Value = DepartmentService.SetupModeRoom, Text = "Room (optional parent / independent)" }
                };
            }

            return new SelectList(items, "Value", "Text", selected);
        }


        [HttpGet]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult PreviewRequisitionFlow(int id, int? parentDepartmentId = null, string mode = null)
        {
            RequisitionFlowMode parsedMode;
            if (!Enum.TryParse(mode, true, out parsedMode))
            {
                parsedMode = RequisitionFlowMode.InheritParent;
            }

            var preview = BuildInheritedFlowPreview(id, parentDepartmentId, parsedMode, parentExplicit: true);
            if (preview == null)
            {
                return HttpNotFound();
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

        private void PopulateInheritedFlowPreview(int departmentId, int? parentDepartmentId, RequisitionFlowMode mode)
        {
            var preview = BuildInheritedFlowPreview(departmentId, parentDepartmentId, mode, parentExplicit: false);
            ViewBag.InheritedFlowPreview = preview;
        }

        private ResolvedRequisitionFlowPreviewVm BuildInheritedFlowPreview(
            int departmentId,
            int? parentDepartmentId,
            RequisitionFlowMode mode,
            bool parentExplicit)
        {
            var entity = UnitOfWork.Repository<Department>().GetById(departmentId);
            if (entity == null)
            {
                return null;
            }

            var probe = new Department
            {
                Id = entity.Id,
                Name = entity.Name,
                Code = entity.Code,
                DepartmentKind = entity.DepartmentKind,
                ParentDepartmentId = parentExplicit ? parentDepartmentId : entity.ParentDepartmentId,
                RequisitionFlowMode = mode,
                CustomStageRoleIds = entity.CustomStageRoleIds,
                CustomStageUserIds = entity.CustomStageUserIds,
                IsRequisitionTarget = entity.IsRequisitionTarget,
                IsActive = entity.IsActive,
                OrganizationId = entity.OrganizationId
            };

            if (mode == RequisitionFlowMode.Custom)
            {
                var roles = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(probe.CustomStageRoleIds);
                var users = ApprovalWorkflowSettingsHelper.ParseStageUserIds(probe.CustomStageUserIds);
                var roleLookup = BuildRoleNameLookup();
                var summary = roles.Count == 0
                    ? "No approval stages configured (auto-approve)."
                    : ApprovalWorkflowSettingsHelper.BuildStageSummary(roles, roleLookup);
                return new ResolvedRequisitionFlowPreviewVm
                {
                    HasApprovers = roles.Count > 0,
                    SourceKind = "CustomDepartment",
                    SourceLabel = "Custom stages on " + (probe.Name ?? "this department"),
                    Summary = summary,
                    Stages = BuildResolvedStages(roles, users, roleLookup)
                };
            }

            var orgDefault = ApprovalWorkflowHelper.GetProcessConfiguration(UnitOfWork, ApprovalProcessCodes.Purchase);
            var detailed = DepartmentRequisitionFlowResolver.ResolveDetailed(
                probe,
                id => UnitOfWork.Repository<Department>().GetById(id),
                orgDefault);
            var config = detailed.Configuration ?? orgDefault;
            var stageRoles = config.StageRoleIds ?? new List<int>();
            var stageUsers = config.StageUserIds ?? new List<string>();
            var lookup = BuildRoleNameLookup();
            var inheritSummary = stageRoles.Count == 0
                ? "No approvers configured for this inheritance path."
                : ApprovalWorkflowSettingsHelper.BuildStageSummary(stageRoles, lookup);

            return new ResolvedRequisitionFlowPreviewVm
            {
                HasApprovers = stageRoles.Count > 0,
                SourceKind = detailed.SourceKind,
                SourceLabel = detailed.SourceLabel,
                Summary = inheritSummary,
                Stages = BuildResolvedStages(stageRoles, stageUsers, lookup)
            };
        }

        private static IList<ResolvedRequisitionFlowStageVm> BuildResolvedStages(
            IList<int> roleIds,
            IList<string> userIds,
            IDictionary<int, string> roleLookup)
        {
            var stages = new List<ResolvedRequisitionFlowStageVm>();
            var roles = roleIds ?? new List<int>();
            var users = userIds ?? new List<string>();
            for (var i = 0; i < roles.Count; i++)
            {
                var roleId = roles[i];
                var roleName = ApprovalWorkflowSettingsHelper.ResolveRoleName(roleLookup, roleId);
                var userId = i < users.Count ? users[i] : null;
                var label = roleName;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    label = roleName + " (" + userId + ")";
                }

                stages.Add(new ResolvedRequisitionFlowStageVm
                {
                    StageNumber = i + 1,
                    RoleId = roleId,
                    RoleName = roleName,
                    UserId = userId,
                    DisplayLabel = label
                });
            }

            return stages;
        }

        private SelectList BuildOrganizationalParentSelectList(int excludeDepartmentId, int? selectedParentDepartmentId)
        {
            var parents = _departmentService.GetOrganizationalParentCandidates(excludeDepartmentId)
                .Select(x => new
                {
                    x.Id,
                    Display = (x.Code ?? string.Empty) + " â€” " + (x.Name ?? string.Empty)
                })
                .ToList();
            return new SelectList(parents, "Id", "Display", selectedParentDepartmentId);
        }

        private SelectList BuildAdminParentDepartmentSelectList(int? selectedParentDepartmentId)
        {
            var parents = _departmentService.GetOrganizationalParentCandidates(0)
                .Where(x => x.DepartmentKind == DepartmentKind.Administrative && !x.ParentDepartmentId.HasValue)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    Display = (x.Code ?? string.Empty) + " â€” " + (x.Name ?? string.Empty)
                })
                .ToList();
            return new SelectList(parents, "Id", "Display", selectedParentDepartmentId);
        }
    }
}
