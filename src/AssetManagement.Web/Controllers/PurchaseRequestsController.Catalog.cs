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
                var picker = service.GetCatalogPicker();
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
