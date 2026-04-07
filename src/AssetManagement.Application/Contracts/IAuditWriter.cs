namespace AssetManagement.Application.Contracts
{
    public interface IAuditWriter
    {
        void Write(string action, string entityType, string entityId, string oldValues, string newValues);
    }
}
