namespace AssetManagement.Application.DTOs
{
    /// <summary>Active org-structure counts per kind (platform org dashboard).</summary>
    public class DepartmentKindCountsDto
    {
        /// <summary>Top-level Administrative departments (no parent).</summary>
        public int Departments { get; set; }

        /// <summary>Administrative rows that have a parent (legacy; normally 0).</summary>
        public int NestedAdministrative { get; set; }

        public int SubDepartments { get; set; }

        public int Rooms { get; set; }

        public int Grades { get; set; }

        public int Classes { get; set; }

        /// <summary>All active rows (sum of the figures above).</summary>
        public int ActiveTotal { get; set; }

        /// <summary>Soft-deleted rows (IsActive = 0), excluded from the figures above.</summary>
        public int InactiveTotal { get; set; }
    }
}
