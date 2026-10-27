using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>rel1013a: asset tag prefix settings (tenant default + department / sub-department / room overrides). Settings.Manage only (class attribute); every read and write goes through the tenant-scoped repositories.</summary>
    public partial class SettingsController
    {
        [HttpGet]
        public ActionResult AssetTags(int? levelScopeId = null, bool levelPreview = false, string departmentPrefix = null, string subDepartmentPrefix = null, string roomPrefix = null, string excludedCodes = null)
        {
            var model = BuildAssetTagSettings(null);
            var levels = MakeLevelOptions(departmentPrefix, subDepartmentPrefix, roomPrefix, excludedCodes);
            model.LevelScopeId = levelScopeId;
            model.Level.DepartmentPrefix = levels.DepartmentOverride;
            model.Level.SubDepartmentPrefix = levels.SubDepartmentOverride;
            model.Level.RoomPrefix = levels.RoomOverride;
            model.Level.ExcludedCodes = string.Join(",", levels.ExcludedDepartmentIds.OrderBy(x => x));
            if (levelPreview)
            {
                var organizationId = ResolveCurrentOrganizationId();
                var departments = UnitOfWork.Repository<Department>().GetAll().ToList(); // tenant-scoped
                var byId = departments.ToDictionary(x => x.Id);
                if (levelScopeId.HasValue && !byId.ContainsKey(levelScopeId.Value))
                {
                    return HttpNotFound();
                }

                var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                    .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
                var ids = levelScopeId.HasValue
                    ? AssetTagRenamePlanner.GetSubtree(levelScopeId.Value, departments)
                    : new HashSet<int>(departments.Where(x => x.IsActive).Select(x => x.Id));
                // One pseudo-candidate per unit so the columns list every code in scope (counts are units, not assets).
                var units = ids.Select(id => new AssetTagRenameCandidate { DepartmentId = id, AssetTag = "-" }).ToList();
                model.Level.Codes = AssetTagRenamePlanner.GetLevelCodes(units, byId, levels);
                var prefix = AssetTagRenamePlanner.BuildLevelPrefix(levelScopeId, byId, AssetTagPrefixResolver.GetTenantPrefix(settings), levels);
                var store = DependencyResolver.Current.GetService<IAssetTagSequenceStore>();
                var next = store == null || !organizationId.HasValue ? 1 : store.PeekNext(organizationId.Value, prefix);
                model.Level.NextTag = AssetTagPrefixResolver.FormatTag(prefix, next);
                model.Level.Previewed = true;
            }

            return View(model);
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

            var prepPrefix = AssetTagPrefixResolver.NormalizePrefix(model.PrepPrefix);
            if (!string.IsNullOrWhiteSpace(model.PrepPrefix) && prepPrefix == null)
            {
                ModelState.AddModelError("PrepPrefix", "Prep prefix: use letters, digits and hyphens only.");
            }

            var seniorPrefix = AssetTagPrefixResolver.NormalizePrefix(model.SeniorPrefix);
            if (!string.IsNullOrWhiteSpace(model.SeniorPrefix) && seniorPrefix == null)
            {
                ModelState.AddModelError("SeniorPrefix", "Senior school prefix: use letters, digits and hyphens only.");
            }

            var departmentRepository = UnitOfWork.Repository<Department>();
            var departments = departmentRepository.GetAll().ToDictionary(x => x.Id);
            var postedRows = PostedTagRows(model, departments);
            var posted = postedRows.ToDictionary(x => x.Key, x => x.Value.Override);
            foreach (var pair in posted)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value) && AssetTagPrefixResolver.NormalizePrefix(pair.Value) == null)
                {
                    ModelState.AddModelError(string.Empty, "Override for " + departments[pair.Key].Name + ": use letters, digits and hyphens only.");
                }
            }

            if (!ModelState.IsValid)
            {
                var invalid = BuildAssetTagSettings(postedRows);
                invalid.Enabled = model.Enabled;
                invalid.TenantPrefix = model.TenantPrefix;
                invalid.PrepPrefix = model.PrepPrefix;
                invalid.SeniorPrefix = model.SeniorPrefix;
                invalid.UseDepartmentSegment = model.UseDepartmentSegment;
                invalid.UseSubDepartmentSegment = model.UseSubDepartmentSegment;
                invalid.UseRoomSegment = model.UseRoomSegment;
                return View(invalid);
            }

            var settingRepository = UnitOfWork.Repository<SystemSetting>();
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(settingRepository.GetAll());
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.EnabledSettingKey, model.Enabled.ToString(), "Generate structured PREFIX-NNN asset tags from the tenant-wide prefix or the segment switches.");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.TenantPrefixSettingKey, tenantPrefix ?? string.Empty, "Tenant-wide asset tag prefix used when no department/sub-department/room override applies. Blank = derived DEPT-ROOM.");
            var previousSectionPrefixes = AssetTagPrefixResolver.GetSectionPrefixes(
                settings.ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase));
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.PrepPrefixSettingKey, prepPrefix ?? string.Empty, "Asset tag prefix for Prep units (used with the room, e.g. PP-GALA-001). Blank = no Prep prefix. Overrides on a department/room still win.");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.SeniorPrefixSettingKey, seniorPrefix ?? string.Empty, "Asset tag prefix for Senior school units. Blank = no Senior school prefix (existing behaviour).");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.UseDepartmentSegmentSettingKey, model.UseDepartmentSegment.ToString(), "Derived asset tags include the top-department segment unless a department/sub-department/room says otherwise (rel1018b).");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.UseSubDepartmentSegmentSettingKey, model.UseSubDepartmentSegment.ToString(), "Derived asset tags include the sub-department segment unless a node says otherwise (rel1018b).");
            UpsertSetting(settingRepository, settings, AssetTagPrefixResolver.UseRoomSegmentSettingKey, model.UseRoomSegment.ToString(), "Derived asset tags include the room segment unless a node says otherwise (rel1018b).");

            var changes = new List<string>();
            foreach (var pair in postedRows)
            {
                var department = departments[pair.Key];
                var row = pair.Value;
                var next = AssetTagPrefixResolver.NormalizePrefix(row.Override);
                var current = AssetTagPrefixResolver.NormalizePrefix(department.AssetTagPrefix);
                var nextDept = ParseTriState(row.UseDepartment);
                var nextSub = ParseTriState(row.UseSubDepartment);
                var nextRoom = ParseTriState(row.UseRoom);
                if (string.Equals(next, current, StringComparison.Ordinal)
                    && nextDept == department.AssetTagUseDepartmentSegment
                    && nextSub == department.AssetTagUseSubDepartmentSegment
                    && nextRoom == department.AssetTagUseRoomSegment)
                {
                    continue;
                }

                changes.Add(department.Id + ":" + (current ?? "-") + "->" + (next ?? "-")
                    + " seg[" + TriStateText(department.AssetTagUseDepartmentSegment) + TriStateText(department.AssetTagUseSubDepartmentSegment) + TriStateText(department.AssetTagUseRoomSegment)
                    + "->" + TriStateText(nextDept) + TriStateText(nextSub) + TriStateText(nextRoom) + "]");
                department.AssetTagPrefix = next;
                department.AssetTagUseDepartmentSegment = nextDept;
                department.AssetTagUseSubDepartmentSegment = nextSub;
                department.AssetTagUseRoomSegment = nextRoom;
                department.UpdatedAt = DateTime.UtcNow;
                departmentRepository.Update(department);
            }

            UnitOfWork.SaveChanges();
            AuditWriter.Write(
                "Settings.AssetTagPrefix",
                nameof(SystemSetting),
                AssetTagPrefixResolver.TenantPrefixSettingKey,
                null,
                "enabled=" + model.Enabled + "; tenant=" + (tenantPrefix ?? "(derived)")
                    + "; prepPrefix=" + (prepPrefix ?? "(none)") + " (was " + (previousSectionPrefixes.ContainsKey(DepartmentSection.Prep) ? previousSectionPrefixes[DepartmentSection.Prep] : "none") + ")"
                    + "; seniorPrefix=" + (seniorPrefix ?? "(none)") + " (was " + (previousSectionPrefixes.ContainsKey(DepartmentSection.SeniorSchool) ? previousSectionPrefixes[DepartmentSection.SeniorSchool] : "none") + ")"
                    + "; segments(D/S/R)=" + model.UseDepartmentSegment + "/" + model.UseSubDepartmentSegment + "/" + model.UseRoomSegment
                    + "; overrides=" + (changes.Count == 0 ? "unchanged" : string.Join(", ", changes)));
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
                return Json(new { ok = true, enabled = false, nextTag = "(structured tags are off)", source = "Tick \"Generate structured tags\" to preview the next tag" }, JsonRequestBehavior.AllowGet);
            }

            var resolution = AssetTagPrefixResolver.ResolveWithOptions(departmentId, departments, AssetTagPrefixResolver.GetOptions(settings));
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

        /// <summary>
        /// rel1018b: effective prefix of every node for the UNSAVED form (overrides, switches, tenant defaults). Read-only:
        /// works on detached copies of the tenant's departments, nothing is saved. Used to auto-fill the boxes below a node.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssetTagEffective(AssetTagSettingsVm model)
        {
            model = model ?? new AssetTagSettingsVm();
            var departments = UnitOfWork.Repository<Department>().GetAll().ToDictionary(x => x.Id);
            var postedRows = PostedTagRows(model, departments);
            var copies = departments.Values.ToDictionary(x => x.Id, x =>
            {
                AssetTagOverrideRowVm row;
                var pending = postedRows.TryGetValue(x.Id, out row);
                return new Department
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    ParentDepartmentId = x.ParentDepartmentId,
                    DepartmentKind = x.DepartmentKind,
                    Section = x.Section,
                    IsActive = x.IsActive,
                    AssetTagPrefix = pending ? row.Override : x.AssetTagPrefix,
                    AssetTagUseDepartmentSegment = pending ? ParseTriState(row.UseDepartment) : x.AssetTagUseDepartmentSegment,
                    AssetTagUseSubDepartmentSegment = pending ? ParseTriState(row.UseSubDepartment) : x.AssetTagUseSubDepartmentSegment,
                    AssetTagUseRoomSegment = pending ? ParseTriState(row.UseRoom) : x.AssetTagUseRoomSegment
                };
            });
            var options = new AssetTagPrefixResolver.Options
            {
                TenantPrefix = AssetTagPrefixResolver.NormalizePrefix(model.TenantPrefix),
                UseDepartmentSegment = model.UseDepartmentSegment,
                UseSubDepartmentSegment = model.UseSubDepartmentSegment,
                UseRoomSegment = model.UseRoomSegment,
                SectionPrefixes = PostedSectionPrefixes(model)
            };
            var rows = copies.Values.Where(x => x.IsActive).Select(x =>
            {
                var r = AssetTagPrefixResolver.ResolveWithOptions(x.Id, copies, options);
                return new { id = x.Id, prefix = r.Prefix, source = r.Source + (r.SourceDepartmentName == null || r.SourceDepartmentId == x.Id ? string.Empty : " (" + r.SourceDepartmentName + ")") };
            }).ToList();
            return Json(new { ok = true, rows });
        }

        /// <summary>Section prefixes (Prep / Senior school) as typed on the unsaved form; blank or unusable = none for that section.</summary>
        private static IDictionary<DepartmentSection, string> PostedSectionPrefixes(AssetTagSettingsVm model)
        {
            var result = new Dictionary<DepartmentSection, string>();
            var prep = AssetTagPrefixResolver.NormalizePrefix(model.PrepPrefix);
            var senior = AssetTagPrefixResolver.NormalizePrefix(model.SeniorPrefix);
            if (prep != null) { result[DepartmentSection.Prep] = prep; }
            if (senior != null) { result[DepartmentSection.SeniorSchool] = senior; }
            return result;
        }

        private static Dictionary<int, AssetTagOverrideRowVm> PostedTagRows(AssetTagSettingsVm model, IDictionary<int, Department> departments)
        {
            return (model.Rows ?? new List<AssetTagOverrideRowVm>())
                .Where(x => x != null && departments.ContainsKey(x.DepartmentId)) // ids from another tenant are not in the scoped list and are ignored
                .GroupBy(x => x.DepartmentId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        internal static bool? ParseTriState(string value)
        {
            bool parsed;
            return !string.IsNullOrWhiteSpace(value) && bool.TryParse(value.Trim(), out parsed) ? parsed : (bool?)null;
        }

        private static string TriStateText(bool? value)
        {
            return value.HasValue ? (value.Value ? "Y" : "N") : "-";
        }

        private static string TriStateValue(bool? value)
        {
            return value.HasValue ? (value.Value ? "true" : "false") : string.Empty;
        }

        private AssetTagSettingsVm BuildAssetTagSettings(IDictionary<int, AssetTagOverrideRowVm> pendingRows)
        {
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
            var departments = UnitOfWork.Repository<Department>().GetAll().Where(x => x.IsActive).ToList();
            var byId = departments.ToDictionary(x => x.Id);
            var options = AssetTagPrefixResolver.GetOptions(settings);
            var sectionPrefixes = options.SectionPrefixes;
            var model = new AssetTagSettingsVm
            {
                Enabled = AssetTagPrefixResolver.IsEnabled(settings),
                TenantPrefix = options.TenantPrefix,
                PrepPrefix = sectionPrefixes.ContainsKey(DepartmentSection.Prep) ? sectionPrefixes[DepartmentSection.Prep] : null,
                SeniorPrefix = sectionPrefixes.ContainsKey(DepartmentSection.SeniorSchool) ? sectionPrefixes[DepartmentSection.SeniorSchool] : null,
                UseDepartmentSegment = options.UseDepartmentSegment,
                UseSubDepartmentSegment = options.UseSubDepartmentSegment,
                UseRoomSegment = options.UseRoomSegment
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
                    var resolution = AssetTagPrefixResolver.ResolveWithOptions(node.Id, byId, options);
                    AssetTagOverrideRowVm pending = null;
                    var hasPending = pendingRows != null && pendingRows.TryGetValue(node.Id, out pending);
                    model.Rows.Add(new AssetTagOverrideRowVm
                    {
                        DepartmentId = node.Id,
                        ParentDepartmentId = parentId,
                        UseDepartment = hasPending ? pending.UseDepartment : TriStateValue(node.AssetTagUseDepartmentSegment),
                        UseSubDepartment = hasPending ? pending.UseSubDepartment : TriStateValue(node.AssetTagUseSubDepartmentSegment),
                        UseRoom = hasPending ? pending.UseRoom : TriStateValue(node.AssetTagUseRoomSegment),
                        Label = label,
                        Depth = depth,
                        KindLabel = node.DepartmentKind.ToString(),
                        IsRoom = AssetTagPrefixResolver.IsRoomKind(node.DepartmentKind),
                        Override = hasPending ? pending.Override : node.AssetTagPrefix,
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