using System.Web;

namespace AssetManagement.Web.Helpers
{
    public static class AuthFlowClientAddressHelper
    {
        public static string Resolve(HttpContextBase context)
        {
            if (context == null || context.Request == null)
            {
                return "unknown";
            }

            var forwarded = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',')[0];
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first.Trim();
                }
            }

            return context.Request.UserHostAddress ?? "unknown";
        }
    }
}
