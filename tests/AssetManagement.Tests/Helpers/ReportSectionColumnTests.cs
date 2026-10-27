using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>QA S1.10: the asset register export has a Section column.</summary>
    [TestFixture]
    public class ReportSectionColumnTests
    {
        [Test]
        public void Label_UsesTheDepartmentSection_AndIsBlankWithoutDepartment()
        {
            var sections = ReportSectionColumn.LoadSections(new List<Department>
            {
                new Department { Id = 1, Name = "Gala", Section = DepartmentSection.Prep },
                new Department { Id = 2, Name = "Grade 2A", Section = DepartmentSection.SeniorSchool }
            });

            Assert.AreEqual("Prep", ReportSectionColumn.Label(1, sections));
            Assert.AreEqual("Senior school", ReportSectionColumn.Label(2, sections));
            Assert.AreEqual(string.Empty, ReportSectionColumn.Label(null, sections));
            Assert.AreEqual(string.Empty, ReportSectionColumn.Label(99, sections));
            Assert.AreEqual(string.Empty, ReportSectionColumn.Label(1, null));
        }
    }
}
