using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetClassificationCreateService
    {
        /// <summary>
        /// Creates a new asset type under an existing (active, same-organisation) category together with its first
        /// sub-type, in one transaction. Categories are never created here. Throws BusinessException on invalid input
        /// or duplicates; nothing is saved in that case.
        /// </summary>
        AssetTypeWithSubTypeCreatedVm CreateTypeWithSubType(AssetTypeWithSubTypeCreateVm model);

        /// <summary>
        /// Inline "Add new..." (requisition form, receiving): a sub-type under an existing type, a type + its first
        /// sub-type under an existing category, or a category + its first type + first sub-type. One transaction,
        /// organisation-scoped, case-insensitive duplicate check under the same parent. Nothing is saved on error.
        /// </summary>
        /// <summary>
        /// Inline create from the requisition form / receiving screen. createdById / createdVia are set by the controller
        /// (signed-in user, ClassificationCreateOrigin) and stored on every new row plus its AuditLog entry; new rows
        /// always get the caller's own organisation.
        /// </summary>
        ClassificationChainCreatedVm CreateChain(ClassificationChainCreateVm model, string createdById, string createdVia);
    }
}