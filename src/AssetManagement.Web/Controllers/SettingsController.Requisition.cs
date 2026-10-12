using System;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Settings &gt; Index: the two requisition policies (classification mode, mixed approval paths) on the existing
    /// settings form. Saving writes both rows (defaults Optional / AutoSplit included).
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

            // PM decision: both rows are written on save, defaults included (Optional / AutoSplit); migration 101
            // seeds them for existing organisations.
            var service = BuildCatalogRequisitionFlowService();
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
