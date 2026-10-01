using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentLabelHelper
    {
        public const string RoomParentOtherOptionValue = "__other__";

        public static string GetEditActionLabel(DepartmentKind kind, bool isTopLevelAdministrative)
        {
            switch (kind)
            {
                case DepartmentKind.Room:
                    return "Edit room";
                case DepartmentKind.SubDepartment:
                    return "Edit sub-department";
                case DepartmentKind.Administrative:
                    return isTopLevelAdministrative ? "Edit administrative department" : "Edit Department";
                default:
                    return "Edit Department";
            }
        }

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
