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
                : new[] { DepartmentKind.Administrative, DepartmentKind.SubDepartment };
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
                    new { Value = DepartmentService.SetupModeSubDepartment, Text = "Sub-unit under admin department" }
                };
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
                .Select(x => new
                {
                    x.Id,
                    Display = (x.Code ?? string.Empty) + " — " + (x.Name ?? string.Empty)
                })
                .ToList();
            return new SelectList(parents, "Id", "Display", selectedParentDepartmentId);
        }
    }
}
