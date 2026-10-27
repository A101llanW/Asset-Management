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

            if (string.Equals(mode, DepartmentSetupModes.Grade, StringComparison.OrdinalIgnoreCase))
            {
                return "Year created.";
            }

            if (string.Equals(mode, DepartmentSetupModes.Class, StringComparison.OrdinalIgnoreCase))
            {
                return "Classroom created.";
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

            if (string.Equals(mode, DepartmentSetupModes.Grade, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: add classrooms (streams) under this year, then assign users or assets to them.";
            }

            if (string.Equals(mode, DepartmentSetupModes.Class, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: review the class details, then assign users or assets to this class.";
            }

            if (string.Equals(mode, DepartmentSetupModes.GradeStreams, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, DepartmentSetupModes.BulkGrades, StringComparison.OrdinalIgnoreCase))
            {
                return "Next step: review the year and classroom structure, then assign users or assets to the new classroom departments.";
            }

            return "Next step: review the department details, then assign users or assets to this department.";
        }
    }
}
