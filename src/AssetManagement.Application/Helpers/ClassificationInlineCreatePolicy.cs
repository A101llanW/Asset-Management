using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>Where a category / type / sub-type was created inline (stored in CreatedVia, migration 110).</summary>
    public static class ClassificationCreateOrigin
    {
        public const string Requisition = "Requisition";
        public const string Receiving = "Receiving";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Requisition, StringComparison.OrdinalIgnoreCase))
            {
                return Requisition;
            }

            return string.Equals(value, Receiving, StringComparison.OrdinalIgnoreCase) ? Receiving : null;
        }
    }

    /// <summary>
    /// Who may create a missing Category / Type / Sub-type inline ("Add new..."). The ONE place that decides it.
    /// Allan 2026-10-28 15:03 EAT: anyone who can use the screen. The requisition form needs Purchases.Create, the
    /// receiving screen Assets.Receive (the same permissions as the screens' own actions); a Head of Department who can
    /// raise requisitions can therefore add them on the requisition form. The server actions
    /// (PurchaseRequestsController.CreateClassification, PurchasesController.CreateClassification) authorise with
    /// PermissionFor(...) and the pages show the option only when CanCreate is true. Settings.Manage / the category
    /// management permission (Assets.Edit) is NOT required.
    /// </summary>
    public static class ClassificationInlineCreatePolicy
    {
        public const string RequisitionScreenPermission = "Purchases.Create";
        public const string ReceivingScreenPermission = "Assets.Receive";

        public static string PermissionFor(string origin)
        {
            switch (ClassificationCreateOrigin.Normalize(origin))
            {
                case ClassificationCreateOrigin.Requisition:
                    return RequisitionScreenPermission;
                case ClassificationCreateOrigin.Receiving:
                    return ReceivingScreenPermission;
                default:
                    return null;
            }
        }

        public static bool CanCreate(string origin, Func<string, bool> hasPermission, bool isSuperAdmin)
        {
            var permission = PermissionFor(origin);
            if (permission == null)
            {
                return false;
            }

            if (isSuperAdmin)
            {
                return true;
            }

            return hasPermission != null && hasPermission(permission);
        }
    }
}
