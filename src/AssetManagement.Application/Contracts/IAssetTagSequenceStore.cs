namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Concurrency-safe per-tenant, per-prefix tag counter (table AssetTagSequence, migration 102).
    /// Allocations run on their own short connection and commit immediately, so two concurrent creates never
    /// get the same number. A failed create leaves a gap, which is fine for tags.
    /// </summary>
    public interface IAssetTagSequenceStore
    {
        /// <summary>Reserves and returns the next number for PREFIX-NNN, never below the highest existing tag with that prefix.</summary>
        int AllocateNext(int organizationId, string prefix);

        /// <summary>The number AllocateNext would return now, without reserving it (for the settings preview).</summary>
        int PeekNext(int organizationId, string prefix);
    }
}