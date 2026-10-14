using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// rel1014a: purchase order (PO) filter options for Bulk Label Print and the asset list.
    /// Options come from the current tenant's purchase records only (IPurchaseService.GetAll is tenant-scoped).
    /// </summary>
    public static class PurchaseFilterOptions
    {
        public static string Label(PurchaseRecordVm purchase)
        {
            if (purchase == null)
            {
                return string.Empty;
            }

            var po = string.IsNullOrWhiteSpace(purchase.PurchaseOrderNumber)
                ? "Purchase #" + purchase.Id.ToString(CultureInfo.InvariantCulture)
                : "PO " + purchase.PurchaseOrderNumber.Trim();
            var parts = new List<string> { po };
            if (!string.IsNullOrWhiteSpace(purchase.SupplierName))
            {
                parts.Add(purchase.SupplierName.Trim());
            }

            if (purchase.PurchaseDate > DateTime.MinValue)
            {
                parts.Add(purchase.PurchaseDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
            }

            return string.Join(" \u00b7 ", parts);
        }

        /// <summary>"All purchase orders" first, then newest purchases first. An id not in the tenant's list is not selected.</summary>
        public static IList<SelectListItem> Build(IEnumerable<PurchaseRecordVm> purchases, int? selectedId)
        {
            var items = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = "All purchase orders", Selected = !selectedId.HasValue }
            };

            foreach (var purchase in (purchases ?? Enumerable.Empty<PurchaseRecordVm>())
                .Where(p => p != null && p.Id > 0)
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.Id))
            {
                items.Add(new SelectListItem
                {
                    Value = purchase.Id.ToString(CultureInfo.InvariantCulture),
                    Text = Label(purchase),
                    Selected = selectedId.HasValue && selectedId.Value == purchase.Id
                });
            }

            return items;
        }

        /// <summary>Returns the id only when it belongs to the tenant's purchase list; otherwise null.</summary>
        public static int? ResolveTenantPurchaseId(IEnumerable<PurchaseRecordVm> purchases, int? purchaseId)
        {
            if (!purchaseId.HasValue || purchaseId.Value <= 0 || purchases == null)
            {
                return null;
            }

            return purchases.Any(p => p != null && p.Id == purchaseId.Value) ? purchaseId : null;
        }
    }
}