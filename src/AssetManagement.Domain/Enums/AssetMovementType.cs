namespace AssetManagement.Domain.Enums
{
    /// <summary>Why an asset's department and/or custodian changed. Stored as int in AssetMovement.MovementType.</summary>
    public enum AssetMovementType
    {
        Edit = 1,
        BulkDepartment = 2,
        RelocateClass = 3,
        RelocateRoom = 4,
        Transfer = 5,
        Assign = 6,
        Return = 7,
        Import = 8,
        Other = 9
    }
}
