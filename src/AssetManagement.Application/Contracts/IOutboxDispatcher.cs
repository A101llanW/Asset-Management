namespace AssetManagement.Application.Contracts
{
    public interface IOutboxDispatcher
    {
        void ProcessPending(int batchSize);
    }
}
