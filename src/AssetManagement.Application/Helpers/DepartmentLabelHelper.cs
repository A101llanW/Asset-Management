namespace AssetManagement.Application.Helpers
{
    public static class DepartmentLabelHelper
    {
        public const string RoomParentOtherOptionValue = "__other__";

        public static string FormatCodeName(string code, string name)
        {
            var safeCode = (code ?? string.Empty).Trim();
            var safeName = (name ?? string.Empty).Trim();
            if (safeCode.Length == 0)
            {
                return safeName;
            }

            if (safeName.Length == 0)
            {
                return safeCode;
            }

            return safeCode + " - " + safeName;
        }
    }
}
