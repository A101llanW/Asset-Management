using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Security;

namespace AssetManagement.Infrastructure.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        public string UserId
        {
            get
            {
                var context = HttpContext.Current;
                return context == null ? null : FormsAuthHelper.GetUserId(context.User);
            }
        }

        public string UserName
        {
            get
            {
                var context = HttpContext.Current;
                return context != null && context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated
                    ? context.User.Identity.Name
                    : null;
            }
        }

        public string IPAddress
        {
            get { return HttpContext.Current != null ? HttpContext.Current.Request.UserHostAddress : null; }
        }
    }
}
