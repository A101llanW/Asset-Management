using System;
using System.Data.SqlClient;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class AuthFlowRateLimiterFailClosedTests
    {
        private sealed class CountingThrowingFactory : ISqlConnectionFactory
        {
            public int Calls { get; private set; }

            public SqlConnection CreateConnection()
            {
                Calls++;
                throw new InvalidOperationException("database down");
            }
        }

        [Test]
        public void MfaSend_DatabaseDown_IsRefused()
        {
            var factory = new CountingThrowingFactory();
            var limiter = new AuthFlowRateLimiterService(factory);
            Assert.IsFalse(limiter.TryAcquireMfaSend("user-1"));
            Assert.AreEqual(1, factory.Calls);
        }

        [Test]
        public void MfaVerify_DatabaseDown_IsRefused_WithAMinuteRemaining()
        {
            var limiter = new AuthFlowRateLimiterService(new CountingThrowingFactory());
            int minutes;
            Assert.IsFalse(limiter.IsMfaVerifyAllowed("user-1", out minutes));
            Assert.AreEqual(1, minutes);
        }

        [Test]
        public void MfaVerifyFailure_And_Clear_DatabaseDown_DoNotThrow()
        {
            var limiter = new AuthFlowRateLimiterService(new CountingThrowingFactory());
            Assert.DoesNotThrow(() => limiter.RecordMfaVerifyFailure("user-1"));
            Assert.DoesNotThrow(() => limiter.ClearMfaVerifyFailures("user-1"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankUserId_HasNoScope_AndNeverTouchesTheDatabase(string userId)
        {
            // Current behaviour: no scope key, so no limit applies. Callers always resolve a real user first.
            var factory = new CountingThrowingFactory();
            var limiter = new AuthFlowRateLimiterService(factory);
            int minutes;
            Assert.IsTrue(limiter.TryAcquireMfaSend(userId));
            Assert.IsTrue(limiter.IsMfaVerifyAllowed(userId, out minutes));
            Assert.AreEqual(0, factory.Calls);
        }

        [Test]
        public void Validate_RejectsCode_OneTickAfterExpiry()
        {
            var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(MfaCodeValidator.Validate(false, "123456", now.AddTicks(-1), "123456", now));
        }

        [Test]
        public void Validate_BypassRejectsWhitespaceOnly_ButAcceptsAnyRealCode()
        {
            var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(MfaCodeValidator.Validate(true, null, null, "\t \r\n", now));
            Assert.IsTrue(MfaCodeValidator.Validate(true, "111111", now.AddMinutes(5), "222222", now));
        }
    }

    /// <summary>
    /// Real counters in a throwaway database (GROKFIX_SCRATCH_DB; DB_NAME() must start with "GrokScratch").
    /// A fresh random user id per test, so no real account is affected.
    /// </summary>
    [TestFixture]
    public class AuthFlowRateLimiterDatabaseTests
    {
        private AuthFlowRateLimiterService _limiter;

        [SetUp]
        public void SetUp()
        {
            var cs = Environment.GetEnvironmentVariable("GROKFIX_SCRATCH_DB");
            if (string.IsNullOrWhiteSpace(cs))
            {
                Assert.Ignore("GROKFIX_SCRATCH_DB not set.");
            }

            using (var c = new SqlConnection(cs))
            using (var cmd = new SqlCommand("SELECT DB_NAME()", c))
            {
                c.Open();
                var name = Convert.ToString(cmd.ExecuteScalar());
                if (!name.StartsWith("GrokScratch", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail("Refusing to write rate-limit rows to '" + name + "'.");
                }
            }

            _limiter = new AuthFlowRateLimiterService(new Factory(cs));
        }

        [Test]
        public void MfaSend_AllowsThree_ThenRefusesTheFourth_PerUser()
        {
            var user = "grokfix-" + Guid.NewGuid().ToString("N");
            Assert.IsTrue(_limiter.TryAcquireMfaSend(user));
            Assert.IsTrue(_limiter.TryAcquireMfaSend(user));
            Assert.IsTrue(_limiter.TryAcquireMfaSend(user));
            Assert.IsFalse(_limiter.TryAcquireMfaSend(user));
            Assert.IsTrue(_limiter.TryAcquireMfaSend("grokfix-" + Guid.NewGuid().ToString("N")), "Another user has its own budget.");
        }

        [Test]
        public void MfaVerify_LocksAfterFiveFailures_AndClearUnlocks()
        {
            var user = "grokfix-" + Guid.NewGuid().ToString("N");
            int minutes;
            for (var i = 0; i < 4; i++)
            {
                _limiter.RecordMfaVerifyFailure(user);
                Assert.IsTrue(_limiter.IsMfaVerifyAllowed(user, out minutes), "Still allowed after " + (i + 1) + " failures");
            }

            _limiter.RecordMfaVerifyFailure(user);
            Assert.IsFalse(_limiter.IsMfaVerifyAllowed(user, out minutes));
            Assert.Greater(minutes, 0);

            _limiter.ClearMfaVerifyFailures(user);
            Assert.IsTrue(_limiter.IsMfaVerifyAllowed(user, out minutes));
        }

        private sealed class Factory : ISqlConnectionFactory
        {
            private readonly string _cs;

            public Factory(string cs)
            {
                _cs = cs;
            }

            public SqlConnection CreateConnection()
            {
                return new SqlConnection(_cs);
            }
        }
    }
}