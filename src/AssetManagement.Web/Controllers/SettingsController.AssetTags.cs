using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>rel1013a: asset tag prefix settings (tenant default + department / sub-department / room overrides). Settings.Manage only (class attribute); every read and write goes through the tenant-scoped repositories.</summary>
    public partial class SettingsController
    {
        [HttpGet]
        public ActionResult AssetTags()
        {
            return View(BuildAssetTagSettings(null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssetTags(AssetTagSettingsVm model)
        {
            model = model ?? new AssetTagSettingsVm();
            var tenantPrefix = AssetTagPrefixResolver.NormalizePrefix(model.TenantPrefix);
            if (!string.IsNullOrWhiteSpace(model.TenantPrefix) && tenantPrefix == null)
            {
                ModelState.AddModelError("TenantPrefix", "Use letters, digits and hyphens only.");
            }

            var departmentRepository = UnitOfWork.Repository<Department>();
            var departments = departmentRepository.GetAll().ToDictionary(x => x.Id);
            var posted = (model.Rows ?? new List<AssetTagOverrideRowVm>())
                .Where(x => x != null && departments.ContainsKey(x.DepartmentId)) // ids from another tenant are not in the scoped list and are ignored
                .GroupBy(x => x.DepartmentId)
                .ToDictionary(g => g.Key, g => g.First().Override);
            foreach (var pair in posted)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value) && AssetTagPrefixResolver.NormalizePrefix(pair.Value) == null)
                {
                    ModelState.AddModelError(string.Empty, "Override for " + departments[pair.Key].Name + ": use letters, digits and hyphens only.");
                }
            }

            if (!ModelState.IsValid)
            {
                var invalid = BuildAssetTagSettings(posted);
                invalid.Enabled = model.Enabled;
                invalid.TenantPrefix = model.TenantPrefix;
                return View(invalid);
            }

            var settingRepository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(settingRepository.GetAll());
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.EnabledSettingKey, model.Enabled.ToString(), "Generate structured PREFIX-NNN asset tags (false = legacy random 10-character tags).");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.TenantPrefixSettingKey, tenantPrefix ?? string.Empty, "Tenant-wide asset tag prefix used when no department/sub-department/room override applies. Blank = derived DEPT-ROOM.");

            var changes = new List<string>();
            foreach (var pair in posted)
            {
                var department = departments[pair.Key];
                var next = AssetTagPrefixResolver.NormalizePrefix(pair.Value);
                var current = AssetTagPrefixResolver.NormalizePrefix(department.AssetTagPrefix);
                if (string.Equals(next, current, StringComparison.Ordinal))
                {
                    continue;
                }

                department.AssetTagPrefix = next;
                department.UpdatedAt = DateTime.UtcNow;
                departmentRepository.Update(department);
                changes.Add(department.Id + ":" + (current ?? "-") + "->" + (next ?? "-"));
            }

            UnitOfWork.SaveChanges();
            AuditWriter.Write(
                "Settings.AssetTagPrefix",
                nameof(SystemSetting),
                AssetTagPrefixResolver.TenantPrefixSettingKey,
                null,
                "enabled=" + model.Enabled + "; tenant=" + (tenantPrefix ?? "(derived)") + "; overrides=" + (changes.Count == 0 ? "unchanged" : string.Join(", ", changes)));
            TempData["SuccessMessage"] = "Asset tag prefix settings saved. Existing tags are unchanged; new assets use the new prefix.";
            return RedirectToAction("AssetTags");
        }

        /// <summary>Preview of the next tag for a department or room. Read-only: does not reserve the number.</summary>
        [HttpGet]
        public ActionResult AssetTagPreview(int? departmentId)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return Json(new { ok = false, message = "Organization context is required." }, JsonRequestBehavior.AllowGet);
            }

            var departments = UnitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id);
            if (departmentId.HasValue && !departments.ContainsKey(departmentId.Value))
            {
                return Json(new { ok = false, message = "Department not found." }, JsonRequestBehavior.AllowGet);
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
            if (!AssetTagPrefixResolver.IsEnabled(settings))
            {
                return Json(new { ok = true, enabled = false, nextTag = "(random 10-character tag)", source = "Structured tags are off" }, JsonRequestBehavior.AllowGet);
            }

            var resolution = AssetTagPrefixResolver.Resolve(departmentId, departments, AssetTagPrefixResolver.GetTenantPrefix(settings));
            var store = DependencyResolver.Current.GetService<IAssetTagSequenceStore>();
            var next = store == null ? 1 : store.PeekNext(organizationId.Value, resolution.Prefix);
            return Json(new
            {
                ok = true,
                enabled = true,
                prefix = resolution.Prefix,
                source = resolution.Source,
                sourceDepartment = resolution.SourceDepartmentName,
                nextTag = AssetTagPrefixResolver.FormatTag(resolution.Prefix, next)
            }, JsonRequestBehavior.AllowGet);
        }

        private AssetTagSettingsVm BuildAssetTagSettings(IDictionary<int, string> pendingOverrides)
        {
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
            var departments = UnitOfWork.Repository<Department>().GetAll().Where(x => x.IsActive).ToList();
            var byId = departments.ToDictionary(x => x.Id);
            var tenantPrefix = AssetTagPrefixResolver.GetTenantPrefix(settings);
            var model = new AssetTagSettingsVm
            {
                Enabled = AssetTagPrefixResolver.IsEnabled(settings),
                TenantPrefix = tenantPrefix
            };

            var children = departments.ToLookup(x => x.ParentDepartmentId.HasValue && byId.ContainsKey(x.ParentDepartmentId.Value) ? x.ParentDepartmentId : null);
            var visited = new HashSet<int>();
            Action<int?, string, int> walk = null;
            walk = (parentId, parentLabel, depth) =>
            {
                foreach (var node in children[parentId].OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!visited.Add(node.Id) || depth > 32)
                    {
                        continue;
                    }

                    var label = parentLabel == null ? node.Name : parentLabel + " > " + node.Name;
                    var resolution = AssetTagPrefixResolver.Resolve(node.Id, byId, tenantPrefix);
                    string pending;
                    model.Rows.Add(new AssetTagOverrideRowVm
                    {
                        DepartmentId = node.Id,
                        Label = label,
                        Depth = depth,
                        KindLabel = node.DepartmentKind.ToString(),
                        IsRoom = AssetTagPrefixResolver.IsRoomKind(node.DepartmentKind),
                        Override = pendingOverrides != null && pendingOverrides.TryGetValue(node.Id, out pending) ? pending : node.AssetTagPrefix,
                        EffectivePrefix = resolution.Prefix,
                        EffectiveSource = resolution.Source + (resolution.SourceDepartmentName == null || resolution.SourceDepartmentId == node.Id ? string.Empty : " (" + resolution.SourceDepartmentName + ")")
                    });
                    walk(node.Id, label, depth + 1);
                }
            };
            walk(null, null, 0);
            return model;
        }
    }
}