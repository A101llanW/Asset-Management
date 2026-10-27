namespace AssetManagement.Application.Helpers
{
    /// <summary>rel1022a: audit actions written on the asset (EntityType Asset) for every printed label.</summary>
    public static class LabelPrintAuditActions
    {
        public const string Print = "Assets.LabelPrint";
        public const string Reprint = "Assets.LabelReprint";

        public static bool IsPrintAction(string action)
        {
            return string.Equals(action, Print, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, Reprint, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
