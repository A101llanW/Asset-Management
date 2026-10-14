using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Requisition approval path on the org's existing catalog nodes (Organization &gt; Asset Categories / Types /
    /// Sub-types) plus the two requisition policy settings. Never creates catalog data.
    /// </summary>
    public interface ICatalogRequisitionFlowService
    {
        /// <summary>Active categories / types / sub-types of the current org for the requisition line pickers.</summary>
        CatalogPickerVm GetCatalogPicker();

        /// <summary>Editor state for one node; null when the node does not exist in this org.</summary>
        CatalogRequisitionFlowEditVm GetFlowForEdit(CatalogFlowScope scope, int scopedEntityId);

        /// <summary>Upserts the node's flow. Stage people must already be validated by the caller (StageApproverRules).</summary>
        void SaveFlow(CatalogRequisitionFlowEditVm model);

        /// <summary>Nodes with an override (Custom / AutoApprove).</summary>
        IList<CatalogRequisitionFlowListItemVm> GetConfiguredFlows();

        RequisitionPolicySettingsVm GetPolicies();

        /// <summary>Writes both setting rows, creating them on first save (lazy seeding).</summary>
        void SavePolicies(RequisitionPolicySettingsVm model);
    }
}
