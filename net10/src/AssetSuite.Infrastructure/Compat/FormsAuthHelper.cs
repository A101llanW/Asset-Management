using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace AssetManagement.Infrastructure.Security
{
    public static class FormsAuthHelper
    {
        public const string UserDataClaimType = "http://nanosoft/assetsuite/userdata";
        public const string UserIdClaimType = "http://nanosoft/assetsuite/userid";

        public static string GetUserId(IPrincipal principal)
        {
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return null;
            }

            var claimsPrincipal = principal as ClaimsPrincipal;
            if (claimsPrincipal != null)
            {
                var userIdClaim = claimsPrincipal.FindFirst(UserIdClaimType);
                if (userIdClaim != null && !string.IsNullOrWhiteSpace(userIdClaim.Value))
                {
                    return userIdClaim.Value;
                }

                var userDataClaim = claimsPrincipal.FindFirst(UserDataClaimType);
                if (userDataClaim != null && !string.IsNullOrWhiteSpace(userDataClaim.Value))
                {
                    return ExtractUserIdFromTicketData(userDataClaim.Value);
                }

                var nameIdentifier = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);
                if (nameIdentifier != null && !string.IsNullOrWhiteSpace(nameIdentifier.Value))
                {
                    return nameIdentifier.Value;
                }
            }

            return null;
        }

        private static string ExtractUserIdFromTicketData(string userData)
        {
            if (string.IsNullOrWhiteSpace(userData))
            {
                return null;
            }

            var pipeIndex = userData.IndexOf('|');
            return pipeIndex < 0 ? userData : userData.Substring(0, pipeIndex);
        }
    }
}
