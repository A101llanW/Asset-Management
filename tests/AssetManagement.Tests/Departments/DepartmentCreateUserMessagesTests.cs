using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentCreateUserMessagesTests
    {
        [Test]
        public void GetCreateSuccessMessage_SubDepartment_UsesGlossary()
        {
            var message = DepartmentCreateUserMessages.GetCreateSuccessMessage(DepartmentService.SetupModeSubDepartment);
            Assert.AreEqual("Sub-department created.", message);
            Assert.IsFalse(message.ToLowerInvariant().Contains("sub-unit"));
        }

        [Test]
        public void GetCreateGuidance_SubDepartment_UsesSubDepartmentNotSubUnit()
        {
            var guidance = DepartmentCreateUserMessages.GetCreateGuidance(DepartmentService.SetupModeSubDepartment);
            StringAssert.Contains("sub-department", guidance.ToLowerInvariant());
            Assert.IsFalse(guidance.ToLowerInvariant().Contains("sub-unit"));
        }

        [Test]
        public void GetCreateGuidance_Room_DoesNotMentionSubUnit()
        {
            var guidance = DepartmentCreateUserMessages.GetCreateGuidance(DepartmentService.SetupModeRoom);
            Assert.IsFalse(guidance.ToLowerInvariant().Contains("sub-unit"));
        }
    }
}
