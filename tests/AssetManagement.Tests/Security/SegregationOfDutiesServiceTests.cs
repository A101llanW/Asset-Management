using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class SegregationOfDutiesServiceTests
    {
        private readonly SegregationOfDutiesService _service = new SegregationOfDutiesService();

        [Test]
        public void EnsureActorIsNotRequester_Throws_WhenSameUser()
        {
            Assert.Throws<BusinessException>(() =>
                _service.EnsureActorIsNotRequester("user-a", "user-a", ApprovalProcessCodes.AssetRequest));
        }

        [Test]
        public void WouldViolateSegregation_ReturnsFalse_ForDifferentUsers()
        {
            Assert.IsFalse(_service.WouldViolateSegregation("user-a", "user-b"));
        }

        [Test]
        public void EnsureActorIsNotRequester_Allows_DifferentUsers()
        {
            _service.EnsureActorIsNotRequester("user-a", "user-b", ApprovalProcessCodes.Purchase);
        }
    }
}
