using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// rel1013b: admin tool that renames EXISTING asset tags to the prefix format. Settings.Manage only (class attribute).
    /// Departments come from the tenant-scoped repository and the store filters every query by organisation id,
    /// so ids from another tenant are never accepted. Old tags are kept as aliases (scan/search fallback) and each batch can be undone.
    /// </summary>
    public partial class SettingsController
    {
        private const int RenamePreviewRows = 500;

        [HttpGet]
        public ActionResult AssetTagRename(int? scopeDepartmentId, bool preview = false, int? batchId = null)
        {
            var organizationId = ResolveCurrentOrganizationId();
            var store = DependencyResolver.Current.GetService<IAssetTagRenameStore>();
            if (!organizationId.HasValue || store == null)
            {
                TempData["ErrorMessage"] = "Organization context is required.";
                return RedirectToAction("Index");
            }

            string error;
            var model = BuildRenameModel(organizationId.Value, store, scopeDepartmentId, preview, out error);
            if (error != null)
            {
                return HttpNotFound();
            }

            if (batchId.HasValue)
            {
                model.ViewBatchId = batchId;
                model.BatchItems = store.GetBatchItems(organizationId.Value, batchId.Value).ToList();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssetTagRenameApply(int? scopeDepartmentId, string planFingerprint)
        {
            var organizationId = ResolveCurrentOrganizationId();
            var store = DependencyResolver.Current.GetService<IAssetTagRenameStore>();
            if (!organizationId.HasValue || store == null)
            {
                return new HttpStatusCodeResult(400);
            }

            string error;
            List<AssetTagRenamePlanItem> plan;
            string scopeLabel;
            if (!TryBuildRenamePlan(organizationId.Value, store, scopeDepartmentId, out plan, out scopeLabel, out error))
            {
                return HttpNotFound();
            }

            if (plan.Count == 0)
            {
                TempData["ErrorMessage"] = "Nothing to rename in " + scopeLabel + ".";
                return RedirectToAction("AssetTagRename", new { scopeDepartmentId });
            }

            if (plan.Count > AssetTagRenamePlanner.MaxBatchSize)
            {
                TempData["ErrorMessage"] = "This scope has " + plan.Count + " tags to rename. Pick a smaller department (max " + AssetTagRenamePlanner.MaxBatchSize + " per batch).";
                return RedirectToAction("AssetTagRename", new { scopeDepartmentId, preview = true });
            }

            if (!string.Equals(Fingerprint(plan), planFingerprint, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "Assets in this scope changed since the preview. Nothing was renamed. Check the new preview and apply again.";
                return RedirectToAction("AssetTagRename", new { scopeDepartmentId, preview = true });
            }

            AssetTagRenameOutcome outcome;
            try
            {
                outcome = store.Apply(organizationId.Value, User.GetUserId(), scopeDepartmentId, scopeLabel, plan);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssetTagRename", new { scopeDepartmentId, preview = true });
            }

            if (!outcome.Success)
            {
                TempData["ErrorMessage"] = outcome.Message;
                return RedirectToAction("AssetTagRename", new { scopeDepartmentId, preview = true });
            }

            AuditWriter.Write(
                "Assets.TagRename.Apply",
                "AssetTagRenameBatch",
                outcome.BatchId.ToString(),
                null,
                "batch=" + outcome.BatchId + "; scope=" + scopeLabel + "; assets=" + outcome.Items.Count + "; sample=" + Sample(outcome.Items));
            UnitOfWork.SaveChanges();
            TempData["SuccessMessage"] = outcome.Message;
            return RedirectToAction("AssetTagRename", new { scopeDepartmentId, batchId = outcome.BatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssetTagRenameUndo(int batchId)
        {
            var organizationId = ResolveCurrentOrganizationId();
            var store = DependencyResolver.Current.GetService<IAssetTagRenameStore>();
            if (!organizationId.HasValue || store == null)
            {
                return new HttpStatusCodeResult(400);
            }

            AssetTagRenameOutcome outcome;
            try
            {
                outcome = store.Undo(organizationId.Value, batchId, User.GetUserId());
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("AssetTagRename");
            }

            if (!outcome.Success)
            {
                TempData["ErrorMessage"] = outcome.Message;
                return RedirectToAction("AssetTagRename", new { batchId });
            }

            AuditWriter.Write(
                "Assets.TagRename.Undo",
                "AssetTagRenameBatch",
                batchId.ToString(),
                "batch=" + batchId + "; assets=" + outcome.Items.Count + "; sample=" + Sample(outcome.Items),
                "undone");
            UnitOfWork.SaveChanges();
            TempData["SuccessMessage"] = outcome.Message;
            return RedirectToAction("AssetTagRename", new { batchId });
        }

        private AssetTagRenameVm BuildRenameModel(int organizationId, IAssetTagRenameStore store, int? scopeDepartmentId, bool preview, out string error)
        {
            var departments = UnitOfWork.Repository<Department>().GetAll().ToList();
            var byId = departments.ToDictionary(x => x.Id);
            error = null;
            if (scopeDepartmentId.HasValue && !byId.ContainsKey(scopeDepartmentId.Value))
            {
                error = "Department not found.";
                return null;
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
            var model = new AssetTagRenameVm
            {
                ScopeDepartmentId = scopeDepartmentId,
                ScopeLabel = scopeDepartmentId.HasValue ? DepartmentPath(scopeDepartmentId.Value, byId) : "Whole organisation",
                StructuredTagsEnabled = AssetTagPrefixResolver.IsEnabled(settings),
                Batches = store.GetBatches(organizationId, 25).ToList()
            };
            model.ScopeOptions = departments.Where(x => x.IsActive)
                .Select(x => new KeyValuePair<int, string>(x.Id, DepartmentPath(x.Id, byId)))
                .OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (preview)
            {
                List<AssetTagRenamePlanItem> plan;
                string label;
                int candidates;
                TryBuildRenamePlan(organizationId, store, scopeDepartmentId, out plan, out label, out error, out candidates);
                model.Previewed = true;
                model.CandidateCount = candidates;
                model.AlreadyConformingCount = candidates - plan.Count;
                model.PlanFingerprint = Fingerprint(plan);
                model.Truncated = plan.Count > RenamePreviewRows;
                model.Items = plan;
            }

            return model;
        }

        private bool TryBuildRenamePlan(int organizationId, IAssetTagRenameStore store, int? scopeDepartmentId, out List<AssetTagRenamePlanItem> plan, out string scopeLabel, out string error)
        {
            int candidates;
            return TryBuildRenamePlan(organizationId, store, scopeDepartmentId, out plan, out scopeLabel, out error, out candidates);
        }

        private bool TryBuildRenamePlan(int organizationId, IAssetTagRenameStore store, int? scopeDepartmentId, out List<AssetTagRenamePlanItem> plan, out string scopeLabel, out string error, out int candidateCount)
        {
            plan = new List<AssetTagRenamePlanItem>();
            candidateCount = 0;
            error = null;
            var departments = UnitOfWork.Repository<Department>().GetAll().ToList(); // tenant-scoped
            var byId = departments.ToDictionary(x => x.Id);
            if (scopeDepartmentId.HasValue && !byId.ContainsKey(scopeDepartmentId.Value))
            {
                scopeLabel = null;
                error = "Department not found.";
                return false;
            }

            scopeLabel = scopeDepartmentId.HasValue ? DepartmentPath(scopeDepartmentId.Value, byId) : "Whole organisation";
            var candidates = scopeDepartmentId.HasValue
                ? store.GetCandidates(organizationId, AssetTagRenamePlanner.GetSubtree(scopeDepartmentId.Value, departments), false)
                : store.GetCandidates(organizationId, null, true);
            candidateCount = candidates.Count;
            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(UnitOfWork.Repository<SystemSetting>().GetAll())
                .ToDictionary(x => x.Key, x => x.Value == null ? null : x.Value.SettingValue, StringComparer.OrdinalIgnoreCase);
            plan = AssetTagRenamePlanner.BuildPlan(
                candidates,
                byId,
                AssetTagPrefixResolver.GetTenantPrefix(settings),
                prefix => store.PeekNext(organizationId, prefix),
                prefix => store.GetTakenTags(organizationId, prefix));
            return true;
        }

        private static string DepartmentPath(int id, IDictionary<int, Department> byId)
        {
            return string.Join(" > ", AssetTagPrefixResolver.GetChain(id, byId).Reverse().Select(x => x.Name));
        }

        private static string Fingerprint(IEnumerable<AssetTagRenamePlanItem> plan)
        {
            var text = string.Join("|", plan.OrderBy(x => x.AssetId).Select(x => x.AssetId + "=" + x.OldTag + ">" + x.Prefix));
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
            }
        }

        private static string Sample(IEnumerable<AssetTagRenamePlanItem> items)
        {
            var list = items.ToList();
            var shown = string.Join(", ", list.Take(20).Select(x => x.OldTag + "->" + x.NewTag));
            return list.Count > 20 ? shown + ", ... (+" + (list.Count - 20) + " more)" : shown;
        }
    }
}