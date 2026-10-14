using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Requisition approval path on existing catalog nodes, edited in place on Organization &gt; Asset Categories /
    /// Types / Sub-types Edit pages (same building blocks and server rules as the department requisition flow).
    /// Gate: Settings.Manage (same as the Approval Matrix); without it the panel is hidden and posts are ignored.
    /// </summary>
    public abstract partial class BaseController
    {
        public const string CatalogRequisitionFlowFormKey = "RequisitionFlow.FlowMode";

        protected ICatalogRequisitionFlowService BuildCatalogRequisitionFlowService()
        {
            return DependencyResolver.Current.GetService<ICatalogRequisitionFlowService>();
        }

        protected bool CanManageCatalogRequisitionFlows()
        {
            return HasPermission("Settings.Manage");
        }

        /// <summary>Sets ViewBag.RequisitionFlow (+ role / person options) for the Edit page panel; posted values win on re-render.</summary>
        protected void PopulateCatalogRequisitionFlowEditor(CatalogFlowScope scope, int scopedEntityId, CatalogRequisitionFlowEditVm posted = null)
        {
            ViewBag.RequisitionFlow = null;
            if (scopedEntityId <= 0 || !CanManageCatalogRequisitionFlows())
            {
                return;
            }

            var service = BuildCatalogRequisitionFlowService();
            if (service == null)
            {
                return;
            }

            CatalogRequisitionFlowEditVm model;
            try
            {
                model = service.GetFlowForEdit(scope, scopedEntityId);
            }
            catch (Exception)
            {
                return; // e.g. migration 100 not applied: the page keeps working without the panel
            }

            if (model == null)
            {
                return;
            }

            if (posted != null)
            {
                model.FlowMode = posted.FlowMode;
                model.Stages = posted.Stages != null && posted.Stages.Count > 0 ? posted.Stages : model.Stages;
                model.PurchaseOrderRaiserRoleId = posted.PurchaseOrderRaiserRoleId;
                model.PurchaseOrderRaiserUserId = posted.PurchaseOrderRaiserUserId;
            }

            ViewBag.RequisitionFlow = model;
            ViewBag.RoleOptions = BuildRoleOptionList();
            PopulateStagePersonOptions();
        }

        /// <summary>
        /// Reads + validates the posted panel. Returns null when the panel was not posted or the user may not manage it.
        /// Scope and node id come from the route (never the post). Errors go to ModelState.
        /// </summary>
        protected CatalogRequisitionFlowEditVm ReadPostedCatalogRequisitionFlow(
            CatalogFlowScope scope,
            int scopedEntityId,
            CatalogRequisitionFlowEditVm posted,
            IList<ApprovalProcessSettingsVm> processes)
        {
            if (posted == null || Request == null || Request.Form[CatalogRequisitionFlowFormKey] == null || !CanManageCatalogRequisitionFlows())
            {
                return null;
            }

            posted.Scope = scope;
            posted.ScopedEntityId = scopedEntityId;
            var process = (processes ?? new List<ApprovalProcessSettingsVm>()).FirstOrDefault(x => x != null);
            posted.Stages = process == null || process.Stages == null ? new List<ApprovalStageSettingsVm>() : process.Stages;
            if (posted.FlowMode == RequisitionFlowMode.Custom && process != null && !process.RequiresApproval)
            {
                posted.FlowMode = RequisitionFlowMode.AutoApprove; // "Requires approval" unticked = no stages
            }

            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Open this page inside a company to set its requisition approval path.");
                return posted;
            }

            var service = BuildCatalogRequisitionFlowService();
            var stored = service == null ? null : service.GetFlowForEdit(scope, scopedEntityId);
            if (stored == null)
            {
                ModelState.AddModelError(string.Empty, CatalogRequisitionFlowResolver.ScopeLabel(scope) + " not found in this organisation.");
                return posted;
            }

            var roleIds = GetOrganizationRoleIdSet(organizationId.Value);
            var findUser = BuildOrgApproverUserFinder(organizationId.Value);
            Action<string, string> addError = (key, message) => ModelState.AddModelError(string.Empty, message);
            if (posted.FlowMode == RequisitionFlowMode.Custom)
            {
                StageApproverRules.ValidateStages(
                    posted.Stages,
                    stored.FlowMode == RequisitionFlowMode.Custom ? stored.StoredStageRoleIds : new List<int>(),
                    organizationId.Value,
                    roleIds,
                    findUser,
                    addError,
                    "RequisitionFlowProcesses.Stages");
            }

            if (posted.FlowMode != RequisitionFlowMode.InheritParent)
            {
                posted.PurchaseOrderRaiserUserId = StageApproverRules.ValidatePurchaseOrderRaiser(
                    posted.PurchaseOrderRaiserRoleId,
                    posted.PurchaseOrderRaiserUserId,
                    stored.StoredPurchaseOrderRaiserRoleId,
                    organizationId.Value,
                    roleIds,
                    findUser,
                    addError);
            }

            if (posted.PurchaseOrderRaiserRoleId.HasValue && posted.PurchaseOrderRaiserRoleId.Value <= 0)
            {
                posted.PurchaseOrderRaiserRoleId = null;
            }

            return posted;
        }

        /// <summary>Saves a validated panel; a business rule failure is added to ModelState and returns false.</summary>
        protected bool TrySaveCatalogRequisitionFlow(CatalogRequisitionFlowEditVm posted)
        {
            if (posted == null)
            {
                return true;
            }

            try
            {
                BuildCatalogRequisitionFlowService().SaveFlow(posted);
                return true;
            }
            catch (AssetManagement.Application.DTOs.BusinessException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return false;
            }
        }
    }
}
