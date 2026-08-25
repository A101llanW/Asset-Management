using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    public enum PasswordResetFailureReason
    {
        None = 0,
        UserNotFound = 1,
        InvalidOrExpiredToken = 2,
        PolicyViolation = 3
    }

    public class PasswordResetResult
    {
        public bool Succeeded { get; set; }

        public PasswordResetFailureReason FailureReason { get; set; }

        public IList<string> PolicyErrors { get; set; }
    }
}
