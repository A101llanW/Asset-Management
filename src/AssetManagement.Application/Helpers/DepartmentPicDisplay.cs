using System;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Index/details label for optional ClassTeacher / RoomCustodian (never required).
    /// </summary>
    public static class DepartmentPicDisplay
    {
        public static string FormatRequisitionOwner(DepartmentVm department)
        {
            if (department == null)
            {
                return Empty;
            }

            if (department.DepartmentKind == DepartmentKind.Class)
            {
                return FormatName(department.ClassTeacherName, department.ClassTeacherUserId, "Class teacher");
            }

            if (department.DepartmentKind == DepartmentKind.Room)
            {
                return FormatName(department.RoomCustodianName, department.RoomCustodianUserId, "Room custodian");
            }

            // Admin / Sub / Grade: no ClassTeacher/RoomCustodian column value.
            return Empty;
        }

        public const string Empty = "—";

        private static string FormatName(string displayName, string userId, string role)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim() + " (" + role + ")";
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId.Trim() + " (" + role + ")";
            }

            return Empty;
        }
    }
}
