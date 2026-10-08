namespace AssetManagement.Domain.Enums
{
    /// <summary>
    /// Which ancestor's requisition flow an Inherit-mode unit uses.
    /// Null / Nearest = legacy behaviour (walk the parent chain to the first Custom/Auto node, else org matrix).
    /// </summary>
    public enum RequisitionInheritSource
    {
        Nearest = 0,

        /// <summary>Nearest Sub-department ancestor (e.g. Room under a Sub).</summary>
        SubDepartment = 1,

        /// <summary>Top-level Administrative ancestor (skips any Sub-department flow in between).</summary>
        Department = 2
    }
}
