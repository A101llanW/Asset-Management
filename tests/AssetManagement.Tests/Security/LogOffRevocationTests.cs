using System;
using System.IO;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Security;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>QA 65d06e1: a cookie copied before Log off still worked. Log off now revokes the ticket server side.</summary>
    [TestFixture]
    public class LogOffRevocationTests
    {
        [Test]
        public void RevokeUserAccessToken_RotatesTheUsersToken_Once()
        {
            var security = new Mock<IAccountSecurityService>();
            Assert.IsTrue(AuthSessionHelper.RevokeUserAccessToken(security.Object, "user-1"));
            security.Verify(x => x.RotateUserAccessToken("user-1"), Times.Once());
            security.Verify(x => x.RotateUserAccessToken(It.Is<string>(id => id != "user-1")), Times.Never());
        }

        [Test]
        public void RevokeUserAccessToken_NoUserOrService_DoesNothing()
        {
            var security = new Mock<IAccountSecurityService>();
            Assert.IsFalse(AuthSessionHelper.RevokeUserAccessToken(security.Object, null));
            Assert.IsFalse(AuthSessionHelper.RevokeUserAccessToken(security.Object, " "));
            Assert.IsFalse(AuthSessionHelper.RevokeUserAccessToken(null, "user-1"));
            security.Verify(x => x.RotateUserAccessToken(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void RevokeUserAccessToken_RotationFailure_NeverBreaksLogOff()
        {
            var security = new Mock<IAccountSecurityService>();
            security.Setup(x => x.RotateUserAccessToken(It.IsAny<string>())).Throws(new InvalidOperationException("db down"));
            Assert.IsFalse(AuthSessionHelper.RevokeUserAccessToken(security.Object, "user-1"));
        }

        [Test]
        public void HasServerAccessToken_OnlyTicketsThatCanBeRevoked()
        {
            Assert.IsTrue(AuthTicketHelper.HasServerAccessToken(AuthTicketHelper.BuildUserData("user-1", 13, "tok", "ua", false)));
            Assert.IsTrue(AuthTicketHelper.HasServerAccessToken(AuthTicketHelper.BuildUserData("user-1", null, "tok", "ua", true)));
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken(AuthTicketHelper.BuildUserData("user-1", 13, null, "ua", false)), "legacy userId-only ticket");
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken("user-1"));
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken("user-1|13|"));
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken("user-1|13||ua"));
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken(""));
            Assert.IsFalse(AuthTicketHelper.HasServerAccessToken(null));
        }

        [Test]
        public void LogOff_RevokesBeforeClearingCookies_AndEveryRequestChecksTheToken()
        {
            var account = Read("src", "AssetManagement.Web", "Controllers", "AccountController.cs");
            var logOff = account.Substring(account.IndexOf("private ActionResult PerformLogOff()", StringComparison.Ordinal));
            logOff = logOff.Substring(0, logOff.IndexOf("return RedirectToLogin(tenantSlug);", StringComparison.Ordinal));
            var revoke = logOff.IndexOf("AuthSessionHelper.RevokeUserAccessToken(_accountSecurityService, User.GetUserId());", StringComparison.Ordinal);
            Assert.Greater(revoke, 0);
            Assert.Less(revoke, logOff.IndexOf("AuthSessionHelper.SignOut(HttpContext);", StringComparison.Ordinal));

            var global = Read("src", "AssetManagement.Web", "Global.asax.cs");
            var check = global.IndexOf("if (!AuthTicketHelper.HasServerAccessToken(ticket.UserData))", StringComparison.Ordinal);
            Assert.Greater(check, 0);
            Assert.Less(check, global.IndexOf("repository.ValidateAccessToken(userId, organizationId, accessToken)", StringComparison.Ordinal));

            var helper = Read("src", "AssetManagement.Web", "Security", "AuthSessionHelper.cs");
            StringAssert.Contains("context.Session.Abandon();", helper);
            StringAssert.Contains("ExpireCookie(context, SessionCookieName());", helper);
        }

        [Test]
        public void SessionCookieName_DefaultsToAspNet()
        {
            Assert.AreEqual("ASP.NET_SessionId", AuthSessionHelper.SessionCookieName());
        }

        private static string Read(params string[] parts)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AssetManagement.sln")) && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "repo root");
            return File.ReadAllText(Path.Combine(dir.FullName, Path.Combine(parts)));
        }
    }
}
