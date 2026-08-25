using System;
using System.Globalization;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    public static class LegalConsentSession
    {
        public static readonly string PendingUserIdSession = "PendingLoginLegal_UserId";
        public static readonly string PendingStartedTicksSession = "PendingLoginLegal_StartedUtcTicks";
        public static readonly string PendingReturnUrlSession = "PendingLoginLegal_ReturnUrl";
        public static readonly string PendingOrganizationIdSession = "PendingLoginLegal_OrganizationId";
        public static readonly string PendingEmailSession = "PendingLoginLegal_Email";
        public static readonly string PendingRememberMeSession = "PendingLoginLegal_RememberMe";

        public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

        public static void Clear(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(PendingUserIdSession);
            session.Remove(PendingStartedTicksSession);
            session.Remove(PendingReturnUrlSession);
            session.Remove(PendingOrganizationIdSession);
            session.Remove(PendingEmailSession);
            session.Remove(PendingRememberMeSession);
        }

        public static string TryReadUserId(HttpSessionStateBase session)
        {
            return session == null ? null : session[PendingUserIdSession] as string;
        }

        public static bool IsFresh(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return false;
            }

            var ticksRaw = session[PendingStartedTicksSession] as string;
            if (string.IsNullOrWhiteSpace(ticksRaw))
            {
                return false;
            }

            long ticks;
            if (!long.TryParse(ticksRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks))
            {
                return false;
            }

            var started = new DateTime(ticks, DateTimeKind.Utc);
            return DateTime.UtcNow - started <= PendingTtl;
        }
    }
}
