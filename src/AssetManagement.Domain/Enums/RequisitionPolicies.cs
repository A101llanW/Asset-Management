namespace AssetManagement.Domain.Enums
{
    /// <summary>Org setting Purchases.Requisition.ClassificationMode.</summary>
    public enum RequisitionClassificationMode
    {
        /// <summary>Lines may be description-only; unclassified lines use the department flow (default, legacy behaviour).</summary>
        Optional = 0,

        /// <summary>Every line must carry at least a Type or Sub-type.</summary>
        RequiredPerLine = 1,

        /// <summary>One header classification (Type or Sub-type) applies to every line without its own pick.</summary>
        RequiredHeader = 2
    }

    /// <summary>Org setting Purchases.Requisition.MixedFlowPolicy.</summary>
    public enum RequisitionMixedFlowPolicy
    {
        /// <summary>Split the form into one requisition per distinct approval path, linked by a PurchaseRequestGroup (default).</summary>
        AutoSplit = 0,

        /// <summary>Reject a form whose lines resolve to different approval paths.</summary>
        Block = 1
    }
}
