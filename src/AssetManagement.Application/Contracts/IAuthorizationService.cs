namespace AssetManagement.Application.Contracts
{
    public interface IAuthorizationService
    {
        bool HasPermission(string userId, string permissionCode);
    }
}
