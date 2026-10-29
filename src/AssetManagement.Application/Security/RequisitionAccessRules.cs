using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Requisition (purchase request) access rules shared by the controller, the views, the requisition list and the
    /// reports, so that a screen never offers more, or less, than the role grants (Allan, 2026-10-29: "if on the roles
    /// they do not have that right, they shouldn't have it").
    /// </summary>
    public static class RequisitionAccessRules
    {
        public const string ViewPermission = "Purchases.View";
        public const string CreatePermission = "Purchases.Create";
        public const string ApprovePermission = "Purchases.Approve";
        public const string EditPermission = "Purchases.Edit";
        public const string CreateForAnyDepartmentPermission = "Purchases.CreateForAnyDepartment";

        /// <summary>
        /// The requisition PDF and attachment follow who can open Details: any of View / Create / Approve. The record
        /// itself is still checked by PurchaseRequestService.GetById (own department, approver, or organisation-wide).
        /// </summary>
        public static readonly string[] DocumentPermissions = { ViewPermission, CreatePermission, ApprovePermission };

        public static bool CanDownloadDocument(Func<string, bool> hasPermission)
        {
            if (hasPermission == null)
            {
                return false;
            }

            foreach (var code in DocumentPermissions)
            {
                if (hasPermission(code))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// "Record purchase order" is shown only when PurchasesController.Create would accept the user: the controller
        /// needs Purchases.View (class) and Purchases.Edit (action), plus the configured PO raiser rule.
        /// </summary>
        public static bool CanShowCreatePurchaseOrder(bool isApproved, int outstandingToOrder, bool canRaisePurchaseOrder, Func<string, bool> hasPermission)
        {
            return isApproved
                && outstandingToOrder > 0
                && canRaisePurchaseOrder
                && hasPermission != null
                && hasPermission(ViewPermission)
                && hasPermission(EditPermission);
        }

        /// <summary>
        /// A requester with a department is locked to it unless the role grants Purchases.CreateForAnyDepartment or the
        /// user is the tenant administrator (Company Admin / support impersonation). Assets.ViewAll alone ("see every
        /// asset") no longer unlocks raising for any department.
        /// </summary>
        public static bool IsDepartmentLocked(bool canCreateForAnyDepartment, bool isTenantAdministrator, int? profileDepartmentId)
        {
            return !canCreateForAnyDepartment && !isTenantAdministrator && profileDepartmentId.HasValue;
        }

        /// <summary>
        /// Approve-any-stage (break-glass) is for the system Company Admin role, or a Platform Admin while support
        /// impersonation is active (that is the only time the authorization service grants them tenant permissions).
        /// A custom role holding Assets.ViewAll is not a break-glass approver.
        /// </summary>
        public static bool CanBypassApprovalStages(IAuthorizationService authorizationService, Role role, string userId)
        {
            if (role == null || !role.IsActive)
            {
                return false;
            }

            if (OrgCapabilities.IsTenantAdministratorRole(role.Name, role.IsSystemRole))
            {
                return true;
            }

            return SystemRoleNames.IsPlatformAdmin(role.Name, role.IsSystemRole)
                && OrgCapabilities.HasOrgWideScope(authorizationService, userId);
        }

        /// <summary>
        /// The requisition list scope (Index, batch, and the pending-approvals report): organisation-wide when the user
        /// bypasses department scope or holds Purchases.Approve; otherwise the user's own department; nothing when a
        /// scoped user has no department.
        /// </summary>
        public static RequisitionListScope ResolveListScope(IDepartmentScopeService departmentScope, bool canApprovePurchases)
        {
            var bypass = canApprovePurchases || (departmentScope != null && departmentScope.BypassesDepartmentScope);
            int? departmentId = null;
            var deny = false;
            if (!bypass)
            {
                departmentId = departmentScope == null ? null : departmentScope.ScopedDepartmentId;
                deny = !departmentId.HasValue;
            }

            return new RequisitionListScope(bypass, departmentId, deny);
        }
    }

    public sealed class RequisitionListScope
    {
        public RequisitionListScope(bool bypassDepartmentScope, int? departmentId, bool denyDepartmentScope)
        {
            BypassDepartmentScope = bypassDepartmentScope;
            DepartmentId = departmentId;
            DenyDepartmentScope = denyDepartmentScope;
        }

        public bool BypassDepartmentScope { get; private set; }

        public int? DepartmentId { get; private set; }

        public bool DenyDepartmentScope { get; private set; }

        public bool Includes(int requisitionDepartmentId)
        {
            if (BypassDepartmentScope)
            {
                return true;
            }

            return !DenyDepartmentScope && DepartmentId.HasValue && DepartmentId.Value == requisitionDepartmentId;
        }
    }
}
