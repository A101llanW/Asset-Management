namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// QA 2026-10-27: the scan page search wording must match what the viewer can actually search.
    /// Full search (signed in with Assets.View): tag, barcode, serial, custodian, department, asset name.
    /// Everyone else: exact tag / barcode / label code only, plus the serial number when the
    /// "AssetScan:ShowSerialToPublic" switch is on.
    /// </summary>
    public static class AssetScanSearchText
    {
        public static string Placeholder(bool fullSearch, bool serialToPublic)
        {
            if (fullSearch)
            {
                return "Tag, serial, custodian, department...";
            }

            return serialToPublic ? "Asset tag, label code or serial number" : "Asset tag or label code";
        }

        public static string EmptyPrompt(bool fullSearch, bool serialToPublic)
        {
            if (fullSearch)
            {
                return "Enter or scan an asset tag, barcode, serial number, custodian, or department.";
            }

            return serialToPublic
                ? "Enter or scan the asset tag, label code or serial number."
                : "Enter or scan the asset tag or label code.";
        }

        public static string NoResultHint(bool fullSearch, bool serialToPublic)
        {
            if (fullSearch)
            {
                return "Try a different tag, serial number, custodian name, or department keyword.";
            }

            return serialToPublic
                ? "Check the tag or serial number printed on the label and try again."
                : "Check the tag printed on the label and try again.";
        }

        public static string SubTitle(bool fullSearch, bool serialToPublic)
        {
            if (fullSearch)
            {
                return "Scan a label or search by tag, serial number, custodian, department, or asset name.";
            }

            return serialToPublic
                ? "Scan a label or type the asset tag or serial number."
                : "Scan a label or type the asset tag.";
        }
    }
}
