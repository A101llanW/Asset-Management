using System;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Settings &gt; Index: the two requisition policies (classification mode, mixed approval paths) on the existing
    /// settings form. Rows are written lazily: only once a value differs from the default or a row already exists.
    /// </summary>
    public partial class SettingsController
    {
        private void PopulateRequisitionPolicies(RequisitionPolicySettingsVm posted)
        {
            ViewBag.RequisitionPolicies = null;
            var service = BuildCatalogRequisitionFlowService();
            if (service == null || !ResolveCurrentOrganizationId().HasValue)
            {
                return;
            }

            try
            {
                var current = service.GetPolicies();
                if (posted != null)
                {
                    current.ClassificationMode = posted.ClassificationMode;
                    current.MixedFlowPolicy = posted.MixedFlowPolicy;
                }

                ViewBag.RequisitionPolicies = current;
                ViewBag.CatalogRequisitionFlows = service.GetConfiguredFlows();
            }
            catch (Exception)
            {
                ViewBag.RequisitionPolicies = null; // settings page must keep working
            }
        }

        private void SaveRequisitionPolicies(RequisitionPolicySettingsVm posted)
        {
            if (posted == null || !ResolveCurrentOrganizationId().HasValue)
            {
                return;
            }

            var service = BuildCatalogRequisitionFlowService();
            var current = service.GetPolicies();
            var isDefault = posted.ClassificationMode == AssetManagement.Domain.Enums.RequisitionClassificationMode.Optional
                && posted.MixedFlowPolicy == AssetManagement.Domain.Enums.RequisitionMixedFlowPolicy.AutoSplit;
            if (!current.IsStored && isDefault)
            {
                return; // lazy seeding: nothing to store while the org keeps the defaults
            }

            try
            {
                service.SavePolicies(posted);
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }
        }
    }
}
