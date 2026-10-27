namespace AssetSuite.Web.Infrastructure;

/// <summary>
/// Mirrors legacy <c>AssetManagement.Web.Security.AuthTicketHelper</c> for cookie user-data format.
/// </summary>
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
}
