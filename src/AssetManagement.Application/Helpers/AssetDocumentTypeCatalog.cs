using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    public static class AssetDocumentTypeCatalog
    {
        public const string CustomOptionValue = "__custom__";

        public const string PhotosMediaGroupName = "Photos & media";

        private static readonly IList<DocumentTypeGroup> StandardGroups = new List<DocumentTypeGroup>
        {
            new DocumentTypeGroup("Acquisition & procurement", new[]
            {
                "Purchase invoice",
                "Purchase order",
                "Delivery note",
                "Goods received note",
                "Supplier quotation",
                "Proforma invoice",
                "Receipt",
                "Import / customs document",
                "Shipping manifest",
                "Packing list"
            }),
            new DocumentTypeGroup("Warranty & support", new[]
            {
                "Warranty certificate",
                "Extended warranty",
                "Service agreement",
                "Support contract",
                "Manufacturer certificate",
                "Registration card",
                "RMA document",
                "Repair report"
            }),
            new DocumentTypeGroup("Condition & inspection", new[]
            {
                "Condition photo",
                "Inspection report",
                "Pre-delivery inspection",
                "Quality assurance certificate",
                "Acceptance certificate",
                "Site acceptance test",
                "Commissioning report",
                "Asset condition report"
            }),
            new DocumentTypeGroup("Insurance & claims", new[]
            {
                "Insurance policy",
                "Insurance certificate",
                "Insurance schedule",
                "Claim form",
                "Claim settlement letter",
                "Loss adjuster report",
                "Police report",
                "Incident photo"
            }),
            new DocumentTypeGroup("Legal & compliance", new[]
            {
                "Title deed / ownership proof",
                "Lease agreement",
                "License certificate",
                "Regulatory approval",
                "Compliance certificate",
                "Environmental clearance",
                "Safety certificate",
                "Calibration certificate",
                "Audit report"
            }),
            new DocumentTypeGroup("Assignment & custody", new[]
            {
                "Assignment form",
                "Handover form",
                "Return form",
                "Transfer authorization",
                "Custody acknowledgment",
                "User acceptance form",
                "Exit clearance"
            }),
            new DocumentTypeGroup("Maintenance & repair", new[]
            {
                "Maintenance record",
                "Service report",
                "Work order",
                "Preventive maintenance checklist",
                "Spare parts list",
                "Fault report",
                "Repair invoice"
            }),
            new DocumentTypeGroup("Disposal & retirement", new[]
            {
                "Disposal authorization",
                "Disposal certificate",
                "Write-off approval",
                "Donation receipt",
                "Scrap certificate",
                "Data wipe certificate"
            }),
            new DocumentTypeGroup("Financial & accounting", new[]
            {
                "Depreciation schedule",
                "Valuation report",
                "Asset register extract",
                "Cost allocation sheet",
                "Tax invoice",
                "Credit note",
                "Budget approval"
            }),
            new DocumentTypeGroup("Technical & specifications", new[]
            {
                "User manual",
                "Technical manual",
                "Installation guide",
                "Configuration document",
                "Datasheet",
                "Bill of materials",
                "Network diagram",
                "Software license key",
                "Serial number record"
            }),
            new DocumentTypeGroup("Photos & media", new[]
            {
                "Asset photo",
                "Serial plate photo",
                "Barcode / QR photo",
                "Damage photo",
                "Location photo",
                "Video recording"
            }),
            new DocumentTypeGroup("General", new[]
            {
                "General",
                "Correspondence",
                "Memo",
                "Meeting minutes",
                "Other supporting document"
            })
        };

        public static IList<DocumentTypeGroup> GetStandardGroups()
        {
            return StandardGroups;
        }

        public static IList<string> GetAllSuggestedTypes(IEnumerable<string> additionalTypes = null)
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in StandardGroups)
            {
                foreach (var type in group.Types)
                {
                    types.Add(type);
                }
            }

            if (additionalTypes != null)
            {
                foreach (var type in additionalTypes)
                {
                    var normalized = NormalizeType(type);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        types.Add(normalized);
                    }
                }
            }

            return types
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string NormalizeType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length > 100 ? trimmed.Substring(0, 100) : trimmed;
        }

        public static bool IsPhotoMediaType(string documentType)
        {
            var normalized = NormalizeType(documentType);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            foreach (var group in StandardGroups)
            {
                if (!string.Equals(group.Name, PhotosMediaGroupName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var type in group.Types)
                {
                    if (string.Equals(type, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string FormatPhotoDocumentType(string baseType, string photoName)
        {
            var normalizedPhotoName = NormalizeType(photoName);
            if (!string.IsNullOrWhiteSpace(normalizedPhotoName))
            {
                return normalizedPhotoName;
            }

            return NormalizeType(baseType) ?? "General";
        }

        public sealed class DocumentTypeGroup
        {
            public DocumentTypeGroup(string name, IEnumerable<string> types)
            {
                Name = name;
                Types = types == null
                    ? new List<string>()
                    : types.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            }

            public string Name { get; private set; }

            public IList<string> Types { get; private set; }
        }
    }
}
