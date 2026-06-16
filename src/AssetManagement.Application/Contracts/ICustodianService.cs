namespace AssetManagement.Application.Contracts
{
    public interface ICustodianService
    {
        void AcknowledgeReceipt(int assetId, string custodianUserId);

        void RequestReturn(int assetId, string custodianUserId, string notes);
    }
}
