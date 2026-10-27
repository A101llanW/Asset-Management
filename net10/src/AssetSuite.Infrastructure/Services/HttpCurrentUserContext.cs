using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Compat;
using AssetManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace AssetManagement.Infrastructure.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string UserId
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context == null ? null : FormsAuthHelper.GetUserId(context.User);
            }
        }

        public string UserName
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context != null && context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated
                    ? context.User.Identity.Name
                    : null;
            }
        }

        public string IPAddress
        {
            get
            {
                var context = _httpContextAccessor.HttpContext;
                return context?.Connection.RemoteIpAddress?.ToString();
            }
        }
    }
}
