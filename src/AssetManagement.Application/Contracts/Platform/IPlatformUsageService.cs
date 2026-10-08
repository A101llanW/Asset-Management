using AssetManagement.Application.ViewModels.Platform;

namespace AssetManagement.Application.Contracts.Platform
{
    public interface IPlatformUsageService
    {
        PlatformUsagePageVm GetUsage(bool bypassCache);
    }
}