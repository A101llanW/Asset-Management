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

    /// <summary>
    /// Inline "Add new..." on the requisition form and the receiving screen (migration-free; existing tables). One of:
    /// - a sub-type under an existing type (AssetTypeId set),
    /// - a type with its first sub-type under an existing category (CategoryId set, TypeName),
    /// - a category with its first type and first sub-type (CategoryName, TypeName).
    /// A type is never created without a sub-type and a category never without a type and sub-type (rule C5 / 692e28e:
    /// a type with no active sub-types cannot be requisitioned or received).
    /// </summary>
    public class ClassificationChainCreateVm
    {
        public int? CategoryId { get; set; }

        [StringLength(200)]
        public string CategoryName { get; set; }

        public int? AssetTypeId { get; set; }

        [StringLength(200)]
        public string TypeName { get; set; }

        [StringLength(200)]
        public string SubTypeName { get; set; }
    }

    public class ClassificationChainCreatedVm : AssetTypeWithSubTypeCreatedVm
    {
        public bool CategoryCreated { get; set; }
        public bool TypeCreated { get; set; }
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