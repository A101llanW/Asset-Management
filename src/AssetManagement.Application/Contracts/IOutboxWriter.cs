namespace AssetManagement.Application.Contracts
{
    public interface IOutboxWriter
    {
        void Enqueue(string messageType, string payloadJson);
    }
}
