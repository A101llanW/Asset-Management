using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class InvitationTokenHelperTests
    {
        [Test]
        public void GenerateToken_ReturnsNonEmptyUrlSafeValue()
        {
            var token = InvitationTokenHelper.GenerateToken();

            Assert.That(string.IsNullOrWhiteSpace(token), Is.False);
            Assert.That(token.Contains("+"), Is.False);
            Assert.That(token.Contains("/"), Is.False);
        }

        [Test]
        public void ComputeTokenHash_IsDeterministicForSameToken()
        {
            var token = InvitationTokenHelper.GenerateToken();

            var first = InvitationTokenHelper.ComputeTokenHash(token);
            var second = InvitationTokenHelper.ComputeTokenHash(token);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void ComputeTokenHash_ChangesWhenTokenChanges()
        {
            var firstToken = InvitationTokenHelper.GenerateToken();
            var secondToken = InvitationTokenHelper.GenerateToken();

            Assert.That(
                InvitationTokenHelper.ComputeTokenHash(firstToken),
                Is.Not.EqualTo(InvitationTokenHelper.ComputeTokenHash(secondToken)));
        }
    }
}
