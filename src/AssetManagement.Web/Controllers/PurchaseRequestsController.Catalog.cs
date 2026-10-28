using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Catalog requisition flows on the requisition form: line pickers fed by the org's existing categories /
    /// types / sub-types, a live per-line approval path preview, and the batch summary for auto-split forms.
    /// </summary>
    public partial class PurchaseRequestsController
    {
        /// <summary>Same classification + resolution as submit (PreviewPaths); nothing is saved.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public JsonResult PreviewLinePaths(PurchaseRequestCreateVm model)
        {
            var preview = _purchaseRequestService.PreviewPaths(model);
            return Json(new
            {
                error = preview.Error,
                mode = preview.ClassificationMode.ToString(),
                policy = preview.MixedFlowPolicy.ToString(),
                mixed = preview.IsMixed,
                willBlock = preview.WillBlock,
                lines = preview.Lines.Select(x => new { lineNumber = x.LineNumber, groupNumber = x.GroupNumber, label = x.SourceLabel }).ToList(),
                groups = preview.Groups.Select(x => new
                {
                    groupNumber = x.GroupNumber,
                    label = x.SourceLabel,
                    summary = BuildApprovalConfigurationSummary(x.Configuration),
                    lineNumbers = x.LineNumbers
                }).ToList()
            });
        }

        /// <summary>Batch summary: every requisition auto-split from one form (PurchaseRequestGroup).</summary>
        public ActionResult Batch(int id, string returnUrl = null)
        {
            var batch = _purchaseRequestService.GetBatch(id);
            if (batch == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(batch);
        }

        // Rule C5 lookup filled by PopulateCatalogPickers: every active type of the org, and those with active sub-types.
        private ISet<int> _catalogPickerTypeIds;
        private ISet<int> _catalogPickerTypeIdsWithSubTypes;

        /// <summary>
        /// Rule C5 message order (QA 2026-10-28): an untagged line whose type has no active sub-types gets the clear
        /// "no active sub-types" message instead of the generic "choose the asset type and sub-type" from the view
        /// model, before ModelState stops the post. PurchaseRequestService.SubmitBatch still rejects it as a backstop.
        /// </summary>
        private void ApplyNoActiveSubTypeMessages(PurchaseRequestCreateVm model)
        {
            if (model == null || _catalogPickerTypeIds == null || _catalogPickerTypeIdsWithSubTypes == null)
            {
                return;
            }

            var lines = model.FindUntaggedLinesWithTypeLackingSubTypes(typeId =>
                _catalogPickerTypeIds.Contains(typeId) ? _catalogPickerTypeIdsWithSubTypes.Contains(typeId) : (bool?)null);
            foreach (var index in lines)
            {
                var key = "Lines[" + index + "].AssetSubTypeId";
                ModelState state;
                if (ModelState.TryGetValue(key, out state))
                {
                    state.Errors.Clear();
                }

                ModelState.AddModelError(key, PurchaseRequestCreateVm.NoActiveSubTypesMessage(index + 1));
            }
        }

        private void PopulateCatalogPickers()
        {
            ViewBag.CatalogPickerJson = "{\"categories\":[],\"types\":[],\"subTypes\":[]}";
            ViewBag.RequisitionPolicies = null;
            ViewBag.PreviewLinePathsUrl = TenantUrlHelper.TenantRouteUrl(Url, "PreviewLinePaths", "PurchaseRequests");
            var service = BuildCatalogRequisitionFlowService();
            if (service == null)
            {
                return;
            }

            try
            {
                var allTypes = service.GetCatalogPicker();
                _catalogPickerTypeIds = new HashSet<int>(allTypes.Types.Select(x => x.Id));
                _catalogPickerTypeIdsWithSubTypes = allTypes.TypeIdsWithSubTypes();
                // Rule C5: a type with no active sub-types is not offered (Allan 2026-10-28 09:33 EAT).
                var picker = allTypes.RequisitionableOnly();
                ViewBag.RequisitionPolicies = service.GetPolicies();
                ViewBag.CatalogPickerJson = new JavaScriptSerializer().Serialize(new
                {
                    categories = picker.Categories.Select(x => new { id = x.Id, name = x.Name }),
                    types = picker.Types.Select(x => new { id = x.Id, name = x.Name, parentId = x.ParentId }),
                    subTypes = picker.SubTypes.Select(x => new { id = x.Id, name = x.Name, parentId = x.ParentId })
                });
            }
            catch (System.Exception)
            {
                // Catalog lookups are optional on the form; submit still validates server-side.
            }
        }
    }
}
