namespace AssetManagement.Domain.Enums
{
    public enum AssetStatus
    {
        Requested = 1,
        AwaitingApproval = 2,
        Ordered = 3,
        Received = 4,
        InStore = 5,
        Assigned = 6,
        UnderMaintenance = 7,
        Damaged = 8,
        Lost = 9,
        Stolen = 10,
        Returned = 11,
        Retired = 12,
        Disposed = 13
    }
}
