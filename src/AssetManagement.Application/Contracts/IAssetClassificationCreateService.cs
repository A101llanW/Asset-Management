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
    }
}