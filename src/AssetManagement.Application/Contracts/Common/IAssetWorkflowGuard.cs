namespace AssetManagement.Application.Contracts
{
    public interface IAssetWorkflowGuard
    {
        void EnsureNoBlockingWorkflow(int assetId);
    }
}
