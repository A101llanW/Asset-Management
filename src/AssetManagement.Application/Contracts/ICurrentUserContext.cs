namespace AssetManagement.Application.Contracts
{
    public interface ICurrentUserContext
    {
        string UserId { get; }

        string UserName { get; }

        string IPAddress { get; }
    }
}
