using System.Globalization;

namespace AssetSuite.Web.Helpers;

public static class AuthenticationErrorMessages
{
    public const string GenericLoginFailure = "Invalid email or password.";

    public static string LoginIpRateLimited() =>
        "Too many failed login attempts from your location. Please wait 15 minutes before trying again.";

    public static string LoginAccountLocked(int minutesRemaining) =>
        "Account is locked. Please try again in " + minutesRemaining + " minutes.";

    public static string LoginFailure(string email, string tenantSlug, int remainingAttempts)
    {
        if (remainingAttempts > 1)
        {
            return string.Format(CultureInfo.InvariantCulture, "Invalid login attempt. {0} attempts remaining.", remainingAttempts);
        }

        if (remainingAttempts == 1)
        {
            return "Invalid login attempt. 1 attempt remaining before account lockout.";
        }

        return "Invalid login attempt. Account is now locked for 30 minutes.";
    }
}
