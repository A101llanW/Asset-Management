using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string ReceivePermission = "Assets.Receive";

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
                && HasPurchaseOrderPermission(hasPermission);
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
        /// bypasses department scope or holds Purchases.Approve (every approver, HoDs included: Allan 2026-11-02, answer
        /// 4); otherwise the user's own department and the units below it (the same subtree they may raise for, answer
        /// 2); nothing when a scoped user has no department.
        /// </summary>
        /// <param name="activeDepartments">Active departments as (id, parent id); null = own department only.</param>
        public static RequisitionListScope ResolveListScope(
            IDepartmentScopeService departmentScope,
            bool canApprovePurchases,
            IEnumerable<KeyValuePair<int, int?>> activeDepartments = null)
        {
            var bypass = canApprovePurchases || (departmentScope != null && departmentScope.BypassesDepartmentScope);
            int? departmentId = null;
            var deny = false;
            IEnumerable<int> subtree = null;
            if (!bypass)
            {
                departmentId = departmentScope == null ? null : departmentScope.ScopedDepartmentId;
                deny = !departmentId.HasValue;
                if (departmentId.HasValue && activeDepartments != null)
                {
                    subtree = RequisitionTargetScope.SelfAndActiveDescendants(departmentId.Value, activeDepartments);
                }
            }

            return new RequisitionListScope(bypass, departmentId, deny, subtree);
        }

        /// <summary>
        /// "Receive" on requisition Details is shown only when PurchasesController.ReceiveForRequisition / Receive would
        /// accept the user: Purchases.View (class filter) and Assets.Receive (action filter).
        /// </summary>
        public static bool CanShowReceive(Func<string, bool> hasPermission)
        {
            return hasPermission != null
                && hasPermission(ViewPermission)
                && hasPermission(ReceivePermission);
        }

        /// <summary>
        /// Allan 2026-11-02 (answer 5): raising a purchase order is permission-based, not tied to Finance, Procurement or
        /// a configured raiser. The "PO permission" is what PurchasesController.Create enforces: Purchases.View (class)
        /// and Purchases.Edit (action). A configured "Purchase order raised by" role / person is the expected raiser
        /// (named on the page and notified when the requisition is approved), not an exclusive gate.
        /// </summary>
        public static bool HasPurchaseOrderPermission(Func<string, bool> hasPermission)
        {
            return hasPermission != null
                && hasPermission(ViewPermission)
                && hasPermission(EditPermission);
        }
    }

    public sealed class RequisitionListScope
    {
        private readonly HashSet<int> _departmentIds;

        public RequisitionListScope(bool bypassDepartmentScope, int? departmentId, bool denyDepartmentScope, IEnumerable<int> departmentIds = null)
        {
            BypassDepartmentScope = bypassDepartmentScope;
            DepartmentId = departmentId;
            DenyDepartmentScope = denyDepartmentScope;
            _departmentIds = new HashSet<int>(departmentIds ?? Enumerable.Empty<int>());
            if (departmentId.HasValue)
            {
                _departmentIds.Add(departmentId.Value);
            }
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

            return !DenyDepartmentScope && DepartmentId.HasValue && _departmentIds.Contains(requisitionDepartmentId);
        }
    }
}
