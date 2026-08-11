using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Documents
{
    [TestFixture]
    public class AssetDocumentProcessHelperTests
    {
        [Test]
        public void IncidentTypeRequiresPhoto_ReturnsFalseForLostAndStolen()
        {
            Assert.IsFalse(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Lost));
            Assert.IsFalse(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Stolen));
        }

        [Test]
        public void IncidentTypeRequiresPhoto_ReturnsTrueForDamageFamilyTypes()
        {
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Damaged));
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.FireDamage));
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.WaterDamage));
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Accident));
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Negligence));
            Assert.IsTrue(AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(IncidentType.Misuse));
        }
    }
}
