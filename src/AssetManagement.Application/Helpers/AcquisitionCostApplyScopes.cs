namespace AssetManagement.Application.Helpers
{
    public static class AcquisitionCostApplyScopes
    {
        public const string Individual = "individual";

        public const string SameSubType = "subtype";

        public const string FilteredGroup = "filtered";

        public static bool IsValid(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return false;
            }

            return string.Equals(scope, Individual, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, SameSubType, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, FilteredGroup, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
