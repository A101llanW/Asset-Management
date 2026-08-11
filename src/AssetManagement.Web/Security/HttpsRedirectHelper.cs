using System;
using System.Web;
using AssetManagement.Application.Security;

namespace AssetManagement.Web.Security
{
    public static class HttpsRedirectHelper
    {
        public static bool IsHttpsRequest(HttpRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.IsSecureConnection)
            {
                return true;
            }

            var forwardedProto = request.Headers["X-Forwarded-Proto"];
            return !string.IsNullOrWhiteSpace(forwardedProto)
                && string.Equals(forwardedProto.Trim(), "https", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldRedirectToHttps(HttpRequest request)
        {
            if (!DeploymentSecuritySettings.RequireHttpsRedirect || request == null)
            {
                return false;
            }

            if (request.IsLocal)
            {
                return false;
            }

            return !IsHttpsRequest(request);
        }
    }
}
