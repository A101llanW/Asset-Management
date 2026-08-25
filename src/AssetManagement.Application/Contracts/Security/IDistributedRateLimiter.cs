using System;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IDistributedRateLimiter
    {
        bool TryAcquire(string scopeKey, int maxRequests, TimeSpan window);
    }
}
