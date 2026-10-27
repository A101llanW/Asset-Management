namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// No-op on .NET 10 — dev security output must not ship to production (see production checklist).
    /// </summary>
    public static class SecurityDiagnostics
    {
        public static void LogOneTimeCode(string purpose, string email, string code)
        {
        }

        public static void LogPasswordResetLink(string email, string resetLink)
        {
        }

        public static void LogMfaDevBypass(string email)
        {
        }

        public static void LogInvitationLink(string email, string invitationLink)
        {
        }
    }
}
