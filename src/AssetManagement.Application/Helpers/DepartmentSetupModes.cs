namespace AssetManagement.Application.Helpers
{
    public static class DepartmentSetupModes
    {
        public const string Normal = "Normal";
        public const string SubDepartment = "SubDepartment";
        public const string Room = "Room";
        public const string GradeStreams = "GradeWithStreams";
        public const string BulkGrades = "BulkGrades";
        /// <summary>Create ONE grade (any code, incl. Prep G00) with a Section and sort number.</summary>
        public const string Grade = "Grade";
        /// <summary>Create ONE class (stream) under an existing grade.</summary>
        public const string Class = "Class";
    }
}
