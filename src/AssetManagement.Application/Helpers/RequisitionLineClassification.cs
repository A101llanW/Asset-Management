namespace AssetManagement.Application.Helpers
{
    /// <summary>Optional catalog classification of one requisition line (any level may be null).</summary>
    public sealed class RequisitionLineClassification
    {
        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public bool HasAny
        {
            get { return AssetCategoryId.HasValue || AssetTypeId.HasValue || AssetSubTypeId.HasValue; }
        }

        /// <summary>RequiredPerLine / RequiredHeader minimum level: a Type or a Sub-type.</summary>
        public bool HasTypeOrSubType
        {
            get { return AssetTypeId.HasValue || AssetSubTypeId.HasValue; }
        }

        public static RequisitionLineClassification Create(int? categoryId, int? typeId, int? subTypeId)
        {
            return new RequisitionLineClassification
            {
                AssetCategoryId = Positive(categoryId),
                AssetTypeId = Positive(typeId),
                AssetSubTypeId = Positive(subTypeId)
            };
        }

        public RequisitionLineClassification Clone()
        {
            return Create(AssetCategoryId, AssetTypeId, AssetSubTypeId);
        }

        private static int? Positive(int? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }
    }
}
