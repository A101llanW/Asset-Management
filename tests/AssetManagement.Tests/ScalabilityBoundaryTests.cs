using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Web.Security;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class ScalabilityBoundaryTests
    {
        [Test]
        public void ScanLimiter_UsesDistributedProviderWithTenantAndAddressKey()
        {
            var context = BuildHttpContext("10.0.0.4");
            var limiter = new Mock<IDistributedRateLimiter>();
            limiter.Setup(x => x.TryAcquire(It.IsAny<string>(), 30, TimeSpan.FromMinutes(1))).Returns(true);

            Assert.IsTrue(ScanLookupRateLimiter.TryAcquire(context, limiter.Object));
            limiter.Verify(x => x.TryAcquire("scan-lookup|global|10.0.0.4", 30, TimeSpan.FromMinutes(1)), Times.Once());
        }

        [Test]
        public void CaptchaLimiter_FailsClosedWhenDistributedProviderIsUnavailable()
        {
            var context = BuildHttpContext("10.0.0.5");

            Assert.IsFalse(CaptchaRateLimiter.TryAcquire(context, "generate", null));
        }

        [Test]
        public void FileSystemStorage_RejectsTraversalAndSupportsReadLifecycle()
        {
            var root = Path.Combine(Path.GetTempPath(), "asset-storage-" + Guid.NewGuid().ToString("N"));
            var provider = new FileSystemStorageProvider(root);
            try
            {
                using (var input = new MemoryStream(new byte[] { 1, 2, 3 }))
                {
                    var path = provider.Save(input, "sample.bin", "application/octet-stream", "assets/1");
                    Assert.IsTrue(provider.Exists(path));
                    using (var output = provider.OpenRead(path))
                    {
                        Assert.AreEqual(3, output.Length);
                    }

                    provider.Delete(path);
                    Assert.IsFalse(provider.Exists(path));
                }

                Assert.Throws<InvalidOperationException>(() => provider.GetFullPath("../../outside.bin"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void OrganizationScope_UsesExplicitExecutionContextWithoutHttpContextOrSession()
        {
            var scope = new OrganizationScopeService(
                Mock.Of<ICurrentUserContext>(),
                new ThrowingConnectionFactory());
            scope.SetExecutionContext(new TenantExecutionContext
            {
                OrganizationId = 42,
                IsImpersonating = true,
                IsCompanyAdmin = true,
                ImpersonationReason = "support"
            });

            Assert.AreEqual(42, scope.GetCurrentOrganizationId());
            Assert.IsTrue(scope.IsImpersonating());
            Assert.IsTrue(scope.IsCompanyAdmin());
            Assert.AreEqual("support", scope.GetImpersonationReason());
        }

        [Test]
        public void EntityMap_IncludesInheritedAuditableProperties()
        {
            var map = EntityMapRegistry.GetMap<Asset>();
            Assert.IsTrue(map.ScalarProperties.Any(property => property.Name == "IsActive"));
            Assert.IsTrue(map.ScalarProperties.Any(property => property.Name == "CreatedAt"));
        }

        [Test]
        public void AuthRateLimiter_FailsClosedWhenPersistenceIsUnavailable()
        {
            var limiter = new AuthFlowRateLimiterService(new ThrowingConnectionFactory());
            var minutes = 0;

            Assert.IsFalse(limiter.TryAcquireRegistration("tenant", "10.0.0.6"));
            Assert.IsFalse(limiter.IsMfaVerifyAllowed("user-1", out minutes));
            Assert.AreEqual(1, minutes);
        }

        private static HttpContextBase BuildHttpContext(string address)
        {
            var request = new Mock<HttpRequestBase>();
            request.SetupGet(x => x.UserHostAddress).Returns(address);
            request.SetupGet(x => x.RequestContext).Returns(new System.Web.Routing.RequestContext());
            var context = new Mock<HttpContextBase>();
            context.SetupGet(x => x.Request).Returns(request.Object);
            context.SetupGet(x => x.Items).Returns(new System.Collections.Generic.Dictionary<object, object>());
            return context.Object;
        }

        private sealed class ThrowingConnectionFactory : ISqlConnectionFactory
        {
            public SqlConnection CreateConnection()
            {
                throw new InvalidOperationException("database unavailable");
            }
        }
    }
}
