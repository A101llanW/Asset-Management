namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Shared copy for the import UI and the downloadable Excel Instructions sheet.
    /// </summary>
    public static class AssetImportTemplateInstructions
    {
        public static readonly string[] Lines =
        {
            "Asset import template — instructions for filling in",
            "",
            "Before you start",
            "• Download a fresh template from Import Assets each time (do not reuse an old file with extra columns).",
            "• Work on the Import sheet. Row 1 = column names, row 2 = Required/Optional, row 3 = example only.",
            "• Enter one logical item per row starting at row 4. Bulk classroom rows expand into one asset per unit.",
            "• Do not change or delete row 1 or row 2.",
            "",
            "Required on every row (columns marked * in row 1)",
            "• AssetName — short descriptive name",
            "• AssetCategory — must match a category name (legacy uploads may use Category instead)",
            "• AssetType — must match an asset type name and belong to that category",
            "• Brand, Model, PurchaseDate, AcquisitionCost — legacy opening-balance rows may leave these blank; defaults are applied on import",
            "",
            "School / classroom columns",
            "• Department — Classroom for class assets; Administration or Information Technology for admin/IT rows",
            "• Class — required when Department = Classroom (examples: 2A, 3B, 6B). For admin/IT rows, enter the sub-unit name (examples: Comp Lab - Senior, Reception).",
            "• Quantity — optional explicit unit count. If blank, the importer parses counts from Description (example: 12 units).",
            "",
            "Plain-text columns",
            "• AssetCategory, AssetType, Department, Supplier, AssetSubType",
            "• Leave Department blank to register the asset under organization custody.",
            "",
            "System-generated — do not add columns for these",
            "• Asset tag and QR code are created automatically when the asset is imported (each unit gets a unique random tag).",
            "",
            "Optional columns",
            "• SerialNumber, Description, Currency, TaxAmount, Specifications, warranty dates, insurance fields, depreciation overrides, and other optional fields.",
            "",
            "Pick-list columns",
            "• ConditionOnReceipt, Condition, CurrentStatus, DepreciationMethod, IsInsured, IsLeased",
            "",
            "Column order (Import sheet row 1)",
            "• AssetName, AssetCategory, AssetType, Brand, Model, PurchaseDate, AcquisitionCost, AssetSubType, SerialNumber, Description, Department, Class, Supplier, Currency, TaxAmount, ConditionOnReceipt, SalvageValue, DepreciationMethod, DepreciationStartDate, DepreciationLifeMonths, DepreciationRatePercent, IsInsured, InsuredValue, WarrantyStartDate, WarrantyEndDate, CurrentStatus, Condition, Specifications, IsLeased, PolicyReference, Quantity",
            "",
            "When finished",
            "• Save the workbook and upload it on the Import Assets page (.xlsx recommended; .csv also accepted).",
            "• The first import pass can provision missing departments, categories, and asset types from the file.",
            "• Review the import summary for any skipped rows and fix those rows before re-uploading."
        };
    }
}
