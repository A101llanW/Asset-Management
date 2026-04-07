using System.Web;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Infrastructure.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        public string UserId
        {
            get
            {
                var principal = HttpContext.Current?.User;
                return principal?.Identity?.IsAuthenticated == true
                    ? System.Security.Claims.ClaimsPrincipal.Current?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : null;
            }
        }

        public string UserName => HttpContext.Current?.User?.Identity?.Name;

        public string IPAddress => HttpContext.Current?.Request?.UserHostAddress;
    }
}
