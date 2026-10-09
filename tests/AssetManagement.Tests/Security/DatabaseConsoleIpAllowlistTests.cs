using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class DatabaseConsoleIpAllowlistTests
    {
        [TestCase("*")]
        [TestCase("any")]
        [TestCase("ANY")]
        [TestCase(" Any ")]
        [TestCase("127.0.0.1,*")]
        [TestCase("192.168.30.122; any")]
        [TestCase("::1 *")]
        public void Wildcard_DisablesCheck_AnyIpAllowed(string raw)
        {
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsDisabled(raw));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "203.0.113.9"));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "2001:db8::5"));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "127.0.0.1"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankOrMissing_DefaultsToLoopback(string raw)
        {
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsDisabled(raw));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "127.0.0.1"));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "::1"));
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsAllowed(raw, "192.168.30.122"));
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsAllowed(raw, "203.0.113.9"));
        }

        [Test]
        public void ExplicitList_StillEnforced()
        {
            const string raw = "127.0.0.1,::1,192.168.30.122";
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsDisabled(raw));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "192.168.30.122"));
            Assert.IsTrue(DatabaseConsoleIpAllowlist.IsAllowed(raw, "::1"));
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsAllowed(raw, "203.0.113.9"));
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsAllowed(raw, null));
        }

        [TestCase("anyhost")]
        [TestCase("**")]
        [TestCase("10.0.0.*")]
        public void NearMisses_DoNotDisable(string raw)
        {
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsDisabled(raw));
            Assert.IsFalse(DatabaseConsoleIpAllowlist.IsAllowed(raw, "203.0.113.9"));
        }
    }
}
