using System;

namespace AssetManagement.Web.Security
{
    public static class AuthTicketHelper
    {
        public const string RequirePasswordChangeMarker = "RequirePasswordChange";

        public static string BuildUserData(
            string userId,
            int? organizationId,
            string accessToken,
            string uaHash,
            bool requirePasswordChange)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return userId;
            }

            var orgPart = organizationId.HasValue ? organizationId.Value.ToString() : string.Empty;
            var uaPart = uaHash ?? string.Empty;
            var ticket = string.Format("{0}|{1}|{2}|{3}", userId, orgPart, accessToken, uaPart);
            if (requirePasswordChange)
            {
                ticket += "|" + RequirePasswordChangeMarker;
            }

            return ticket;
        }

        public static string ExtractUserId(string userData)
        {
            string userId;
            int? organizationId;
            string accessToken;
            string uaHash;
            bool requirePasswordChange;
            if (!TryParseUserData(userData, out userId, out organizationId, out accessToken, out uaHash, out requirePasswordChange))
            {
                return userData;
            }

            return userId;
        }

        public static bool TryParseUserData(
            string userData,
            out string userId,
            out int? organizationId,
            out string accessToken,
            out string uaHash,
            out bool requirePasswordChange)
        {
            userId = null;
            organizationId = null;
            accessToken = null;
            uaHash = null;
            requirePasswordChange = false;

            if (string.IsNullOrWhiteSpace(userData))
            {
                return false;
            }

            if (userData.IndexOf('|') < 0)
            {
                userId = userData;
                return true;
            }

            var parts = userData.Split('|');
            if (parts.Length < 4)
            {
                userId = userData;
                return true;
            }

            userId = parts[0];
            int orgId;
            if (!string.IsNullOrWhiteSpace(parts[1]) && int.TryParse(parts[1], out orgId))
            {
                organizationId = orgId;
            }

            accessToken = parts[2];
            uaHash = parts[3];
            requirePasswordChange = parts.Length > 4
                && string.Equals(parts[4], RequirePasswordChangeMarker, StringComparison.Ordinal);
            return !string.IsNullOrWhiteSpace(userId);
        }

        public static bool RequiresPasswordChange(string userData)
        {
            string userId;
            int? organizationId;
            string accessToken;
            string uaHash;
            bool requirePasswordChange;
            if (!TryParseUserData(userData, out userId, out organizationId, out accessToken, out uaHash, out requirePasswordChange))
            {
                return false;
            }

            return requirePasswordChange;
        }
    }
}
