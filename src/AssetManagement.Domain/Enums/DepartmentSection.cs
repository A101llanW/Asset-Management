namespace AssetManagement.Domain.Enums
{
    /// <summary>
    /// Which part of the school a department / grade / class / room belongs to (migration 106).
    /// Stored as an int on Department.Section; every row that existed before the migration is SeniorSchool (0).
    /// </summary>
    public enum DepartmentSection
    {
        SeniorSchool = 0,
        Prep = 1
    }
}
