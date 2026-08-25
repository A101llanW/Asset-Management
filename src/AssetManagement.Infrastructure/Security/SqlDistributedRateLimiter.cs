using System;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    public sealed class SqlDistributedRateLimiter : IDistributedRateLimiter
    {
        private readonly AuthFlowRateLimitRepository _repository;

        public SqlDistributedRateLimiter(ISqlConnectionFactory connectionFactory)
        {
            _repository = new AuthFlowRateLimitRepository(connectionFactory);
        }

        public bool TryAcquire(string scopeKey, int maxRequests, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(scopeKey))
            {
                return true;
            }

            try
            {
                return _repository.TryAcquireCounter(scopeKey, maxRequests, window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Distributed rate limiter unavailable: " + ex.Message);
                return false;
            }
        }
    }
}
