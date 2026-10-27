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
                return FormatName(department.ClassTeacherName, department.ClassTeacherUserId, "Classroom teacher");
            }

            if (department.DepartmentKind == DepartmentKind.Room)
            {
                return FormatName(department.RoomCustodianName, department.RoomCustodianUserId, "Room custodian");
            }

            // Admin / Sub / Grade: show the section heads set on this unit (migration 107), if any.
            var parts = new System.Collections.Generic.List<string>();
            var prep = FormatName(department.PrepHeadName, department.PrepHeadUserId, "Head, Prep");
            if (!string.IsNullOrEmpty(prep))
            {
                parts.Add(prep);
            }

            var senior = FormatName(department.SeniorHeadName, department.SeniorHeadUserId, "Head, Senior school");
            if (!string.IsNullOrEmpty(senior))
            {
                parts.Add(senior);
            }

            return parts.Count == 0 ? Empty : string.Join("; ", parts.ToArray());
        }

        public const string Empty = "";

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
