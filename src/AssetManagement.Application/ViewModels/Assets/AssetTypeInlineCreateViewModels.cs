using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>
    /// rel1019g: inline asset type creation from the asset classification picker. A new type is only created together
    /// with its first sub-type (one transaction), so a type never exists without a sub-type.
    /// </summary>
    public class AssetTypeWithSubTypeCreateVm
    {
        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(200)]
        public string TypeName { get; set; }

        [Required]
        [StringLength(200)]
        public string SubTypeName { get; set; }
    }

    public class AssetTypeWithSubTypeCreatedVm
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int AssetTypeId { get; set; }
        public string AssetTypeName { get; set; }
        public int AssetSubTypeId { get; set; }
        public string AssetSubTypeName { get; set; }
    }
}