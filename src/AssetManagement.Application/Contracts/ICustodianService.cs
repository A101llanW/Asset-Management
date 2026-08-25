namespace AssetManagement.Application.Contracts
{
    public interface ICustodianService
    {
        void RequestReturn(int assetId, string custodianUserId, string notes);
    }
}
