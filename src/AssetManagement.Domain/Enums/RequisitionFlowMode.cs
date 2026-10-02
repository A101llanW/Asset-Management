namespace AssetManagement.Domain.Enums
{
    public enum RequisitionFlowMode
    {
        InheritParent = 0,
        Custom = 1,
        /// <summary>Explicit auto-approve at this node (no stages). Prefer over empty Custom.</summary>
        AutoApprove = 2
    }
}
