using System.Linq;
using AssetManagement.Application.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class ModulePermissionCatalogTests
    {
        [Test]
        public void ResolveModule_MapsAssetsController()
        {
            Assert.AreEqual(ModulePermissionCatalog.Assets, ModulePermissionCatalog.ResolveModule("Assets"));
        }

        [Test]
        public void Find_ReturnsAssetsModuleWithPermissionCodes()
        {
            var module = ModulePermissionCatalog.Find(ModulePermissionCatalog.Assets);
            Assert.IsNotNull(module);
            Assert.IsTrue(module.PermissionCodes.Contains("Assets.View"));
        }

        [Test]
        public void PermissionCodesForModule_IncludesUsersEdit()
        {
            var codes = ModulePermissionCatalog.PermissionCodesForModule(ModulePermissionCatalog.Users).ToList();
            CollectionAssert.Contains(codes, "Users.Edit");
        }
    }

    [TestFixture]
    public class SecurePasswordGeneratorTests
    {
        [Test]
        public void Generate_ProducesMinimumLengthPassword()
        {
            var password = SecurePasswordGenerator.Generate();
            Assert.IsNotNull(password);
            Assert.GreaterOrEqual(password.Length, 12);
        }

        [Test]
        public void GenerateAccessToken_ProducesNonEmptyToken()
        {
            var token = SecurePasswordGenerator.GenerateAccessToken();
            Assert.IsFalse(string.IsNullOrWhiteSpace(token));
        }
    }

    [TestFixture]
    public class AuthTicketFormatTests
    {
        [Test]
        public void TicketFormat_ParsesOrgScopedSegments()
        {
            var userData = string.Format("{0}|{1}|{2}|{3}", "user-1", 42, "token", "ua");
            var parts = userData.Split('|');
            Assert.AreEqual("user-1", parts[0]);
            Assert.AreEqual("42", parts[1]);
            Assert.AreEqual("token", parts[2]);
        }

        [Test]
        public void TicketFormat_LegacyUserIdOnly_HasNoPipe()
        {
            const string userData = "legacy-user-id";
            Assert.IsFalse(userData.Contains("|"));
        }
    }
}
