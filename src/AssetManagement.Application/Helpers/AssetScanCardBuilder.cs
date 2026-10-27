using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// rel1027: the card shown when an asset label QR is scanned ({org}/AssetScan/Lookup?code=TAG; the URL itself is unchanged).
    /// The page is public, so this is the single place that decides what each viewer may see:
    ///   * everyone: name, tag, category &gt; type &gt; sub-type, brand / model (serial only with Assets.View, review S8), location (department, Year, Classroom, room),
    ///     Section, status, condition, date received, photo;
    ///   * signed-out viewers: NO person's name or e-mail at all; the custodian line shows only "In &lt;unit&gt;" (Allan, 2026-10-27);
    ///   * signed in: the custodian's name (e-mail only with Assets.View when no name is set);
    ///   * never to signed-out viewers: e-mail addresses, cost, supplier, insurance or any other financial data;
    ///   * signed in with Financials.View: acquisition cost; with Suppliers.View: supplier.
    /// Wording is display only (Grade -&gt; Year, Class -&gt; Classroom); tags and stored names are untouched.
    /// </summary>
    public static class AssetScanCardBuilder
    {
        public const string Separator = " \u203A ";
        public const string AssignedNoNameText = "Assigned to a staff member";
        public const string SignInToSeeCustodianText = "Sign in to see who has it";
        public const string CompanyCustodyText = "Company custody (no person or location set)";

        private static readonly Regex CamelCaseBoundary = new Regex("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static AssetScanCardVm Build(AssetScanLookupVm lookup, AssetScanViewer viewer)
        {
            if (lookup == null || !lookup.Found)
            {
                return null;
            }

            viewer = viewer ?? AssetScanViewer.Anonymous;
            var card = new AssetScanCardVm
            {
                AssetName = lookup.AssetName,
                AssetTag = lookup.AssetTag,
                StatusText = Humanize(lookup.CurrentStatus.ToString()),
                ConditionText = lookup.Condition.HasValue ? Humanize(lookup.Condition.Value.ToString()) : null,
                CustodianText = CustodianText(lookup, viewer),
                // Review fix S7: serial-plate and location photos only for Assets.View; everyone else gets "Asset photo..." only.
                HasPhoto = !string.IsNullOrWhiteSpace(lookup.PhotoFilePath)
                    && AssetScanPhotoRules.MayServe(lookup.PhotoDocumentType, viewer.IsSignedIn && viewer.CanViewAssetDetails)
            };

            card.Rows.Add(viewer.IsSignedIn
                ? new AssetScanCardRow("custodian", "Custodian", card.CustodianText)
                : new AssetScanCardRow("where", "Where it is", card.CustodianText));

            var path = lookup.LocationPath ?? new List<AssetScanLocationLevel>();
            foreach (var level in path.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                card.Rows.Add(new AssetScanCardRow("location-" + level.Kind.ToString().ToLowerInvariant(), KindLabel(level.Kind), GradeDisplay.ToYear(level.Name.Trim())));
            }

            // QA Q7.2 (2026-10-27): the Section row is shown for every asset with a department, also when the
            // tenant has no active Prep units yet (the asset then reads "Senior school").
            if (lookup.DepartmentId.HasValue)
            {
                card.Rows.Add(new AssetScanCardRow("section", "Section", DepartmentSectionHelper.Label(lookup.Section)));
            }

            AddIfSet(card.Rows, "category", "Category", JoinNonBlank(lookup.CategoryName, lookup.AssetTypeName, lookup.AssetSubTypeName));
            AddIfSet(card.Rows, "brand-model", "Brand / model", JoinWith(" / ", lookup.Brand, lookup.Model));
            if (viewer.IsSignedIn || viewer.ShowSerialToPublic)
            {
                // Review S8 (Allan): serial only for Assets.View in scope; Web.config "AssetScan:ShowSerialToPublic" = true shows it to everyone.
                AddIfSet(card.Rows, "serial", "Serial no.", lookup.SerialNumber);
            }
            card.Rows.Add(new AssetScanCardRow("status", "Status", card.StatusText));
            AddIfSet(card.Rows, "condition", "Condition", card.ConditionText);
            AddIfSet(card.Rows, "received", "Date received", FormatDate(lookup.ReceivedDate ?? lookup.PurchaseDate));

            if (viewer.IsSignedIn && viewer.CanViewFinancials && lookup.AcquisitionCost.HasValue)
            {
                card.RestrictedRows.Add(new AssetScanCardRow("cost", "Acquisition cost", FormatMoney(lookup.AcquisitionCost.Value, lookup.Currency)));
            }

            if (viewer.IsSignedIn && viewer.CanViewSuppliers)
            {
                AddIfSet(card.RestrictedRows, "supplier", "Supplier", lookup.SupplierName);
            }

            return card;
        }

        /// <summary>
        /// Signed in: the named person if assigned. Otherwise (and always for signed-out viewers) "In &lt;room / Classroom / Year /
        /// department&gt;". Signed-out viewers never get a person's name or e-mail.
        /// </summary>
        public static string CustodianText(AssetScanLookupVm lookup, AssetScanViewer viewer)
        {
            viewer = viewer ?? AssetScanViewer.Anonymous;
            var hasPerson = !string.IsNullOrWhiteSpace(lookup.CustodianUserId) || !string.IsNullOrWhiteSpace(lookup.CustodianPersonName);
            if (hasPerson && viewer.IsSignedIn)
            {
                if (!string.IsNullOrWhiteSpace(lookup.CustodianPersonName))
                {
                    return lookup.CustodianPersonName.Trim();
                }

                if (viewer.IsSignedIn && viewer.CanViewAssetDetails && !string.IsNullOrWhiteSpace(lookup.CustodianEmail))
                {
                    return lookup.CustodianEmail.Trim();
                }

                return AssignedNoNameText;
            }

            var unit = (lookup.LocationPath ?? new List<AssetScanLocationLevel>())
                .LastOrDefault(x => x != null && !string.IsNullOrWhiteSpace(x.Name));
            var unitName = unit != null ? unit.Name : lookup.DepartmentName;
            if (string.IsNullOrWhiteSpace(unitName))
            {
                return hasPerson ? SignInToSeeCustodianText : CompanyCustodyText;
            }

            return "In " + GradeDisplay.ToYear(unitName.Trim());
        }

        public static string KindLabel(DepartmentKind kind)
        {
            switch (kind)
            {
                case DepartmentKind.Grade:
                    return "Year";
                case DepartmentKind.Class:
                    return "Classroom";
                case DepartmentKind.Room:
                    return "Room";
                case DepartmentKind.SubDepartment:
                    return "Sub-department";
                default:
                    return "Department";
            }
        }

        /// <summary>"RepairNeeded" -&gt; "Repair needed", "InStore" -&gt; "In store".</summary>
        public static string Humanize(string enumName)
        {
            if (string.IsNullOrWhiteSpace(enumName))
            {
                return enumName;
            }

            var words = CamelCaseBoundary.Split(enumName.Trim());
            return string.Join(" ", words.Select((w, i) => i == 0 ? w : w.ToLowerInvariant()).ToArray());
        }

        private static string FormatDate(DateTime? value)
        {
            if (!value.HasValue || value.Value.Year < 1900)
            {
                return null;
            }

            return value.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        private static string FormatMoney(decimal amount, string currency)
        {
            var text = amount.ToString("N2", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(currency) ? text : currency.Trim().ToUpperInvariant() + " " + text;
        }

        private static string JoinNonBlank(params string[] parts)
        {
            return JoinWith(Separator, parts);
        }

        private static string JoinWith(string separator, params string[] parts)
        {
            var kept = (parts ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            return kept.Length == 0 ? null : string.Join(separator, kept);
        }

        private static void AddIfSet(IList<AssetScanCardRow> rows, string key, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(new AssetScanCardRow(key, label, value));
            }
        }
    }

    /// <summary>rel1027: which stored files the public scan photo endpoint may return (images only, never documents).</summary>
    public static class AssetScanPhotoRules
    {
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/pjpeg", "image/png", "image/gif" };

        public static bool IsAllowedContentType(string contentType)
        {
            return !string.IsNullOrWhiteSpace(contentType)
                && AllowedContentTypes.Contains(contentType.Trim().ToLowerInvariant());
        }

        /// <summary>Review fix S7: the only photo type shown to viewers without Assets.View.</summary>
        public static bool IsPublicDocumentType(string documentType)
        {
            return !string.IsNullOrWhiteSpace(documentType)
                && documentType.Trim().StartsWith("Asset photo", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Assets.View (in scope) may see any scan photo type; everyone else only "Asset photo...".</summary>
        public static bool MayServe(string documentType, bool canViewAssetDetails)
        {
            return canViewAssetDetails || IsPublicDocumentType(documentType);
        }
    }
}
