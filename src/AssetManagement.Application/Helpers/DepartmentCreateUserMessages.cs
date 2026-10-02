using System;

namespace AssetManagement.Application.Helpers
{
    public static class DepartmentCreateUserMessages
    {
        public static string GetCreateSuccessMessage(string setupMode)
        {
            var mode = (setupMode ?? string.Empty).Trim();
            if (string.Equals(mode, DepartmentSetupModes.SubDepartment, StringComparison.OrdinalIgnoreCase))
            {
                return "Sub-department created.";
            }

            if (string.Equals(mode, DepartmentSetupModes.Room, StringComparison.OrdinalIgnoreCase))
            {
                return "Room created.";
            }

            return "Department created.";
        }

        public static string GetCreateGuidance(string setupMode)
        {
            var mode = (setupMode ?? string.Empty).Trim();
            if (string.Equals(mode, DepartmentSetupModes.SubDepartment, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: review the sub-department details, then assign users or assets to this sub-department.";
            }

            if (string.Equals(mode, DepartmentSetupModes.Room, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: review the room details, then assign users or assets to this room.";
            }

            if (string.Equals(mode, DepartmentSetupModes.GradeStreams, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, DepartmentSetupModes.BulkGrades, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: review the grade and class structure, then assign users or assets to the new class departments.";
            }

            return "Next step: review the department details, then assign users or assets to this department.";
        }
    }
}
