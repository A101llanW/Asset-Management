namespace AssetManagement.Domain.Enums
{
    /// <summary>
    /// Which part of the school a department / grade / class / room belongs to (migration 106).
    /// Stored as an int on Department.Section; every row that existed before the migration is SeniorSchool (0).
    /// KG (Kindergarten, 2) was added by migration 111 (CK_Department_Section IN (0, 1, 2)).
    /// </summary>
    public enum DepartmentSection
    {
        SeniorSchool = 0,
        Prep = 1,
        KG = 2
    }
}
