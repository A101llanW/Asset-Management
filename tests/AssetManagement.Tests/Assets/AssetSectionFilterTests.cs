using System.Data.SqlClient;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>Section filter on the asset register (list, counts, room groups) and in the list defaults.</summary>
    [TestFixture]
    public class AssetSectionFilterTests
    {
        [Test]
        public void BuildWhereClause_WithoutSection_HasNoSectionPredicate_SoNothingChanges()
        {
            Assert.IsFalse(AssetQueryService.BuildWhereClause(null).Contains("Section"));
            Assert.IsFalse(AssetQueryService.BuildWhereClause(new AssetFilterVm()).Contains("Section"));
        }

        [TestCase(DepartmentSection.Prep, 1)]
        [TestCase(DepartmentSection.SeniorSchool, 0)]
        public void BuildWhereClause_WithSection_FiltersByTheAssetsDepartmentSection_InTheSameTenant(DepartmentSection section, int value)
        {
            var filter = new AssetFilterVm { Section = section };
            var where = AssetQueryService.BuildWhereClause(filter);

            StringAssert.Contains("a.[DepartmentId] IN (SELECT sd.[Id] FROM [Department] sd", where);
            StringAssert.Contains("sd.[OrganizationId] = a.[OrganizationId]", where);
            StringAssert.Contains("sd.[Section] = @FilterSection", where);
            StringAssert.DoesNotContain("Prep", where, "the value is a parameter, never inlined");

            using (var command = new SqlCommand())
            {
                AssetQueryService.AddFilterParameters(command, filter);
                Assert.AreEqual(value, command.Parameters["@FilterSection"].Value);
            }
        }

        [Test]
        public void AddFilterParameters_AlwaysDefinesFilterSection_NullWhenNoSection()
        {
            using (var command = new SqlCommand())
            {
                AssetQueryService.AddFilterParameters(command, new AssetFilterVm());
                Assert.IsTrue(command.Parameters.Contains("@FilterSection"));
                Assert.AreEqual(System.DBNull.Value, command.Parameters["@FilterSection"].Value);
            }
        }

        [Test]
        public void SectionFilter_IsKeptTogetherWithTheOtherFilters()
        {
            var where = AssetQueryService.BuildWhereClause(new AssetFilterVm { Section = DepartmentSection.Prep, DepartmentId = 5 });
            StringAssert.Contains("a.[DepartmentId] = @FilterDepartmentId", where);
            StringAssert.Contains("sd.[Section] = @FilterSection", where);
            StringAssert.Contains("a.[OrganizationId] = @OrganizationId", where);
        }

        [Test]
        public void ListDefaults_ASectionFilterIsAnExplicitFilter_SoTheUsersOwnDepartmentIsNotForcedOnTop()
        {
            var user = new UserVm { Id = "u1", DepartmentId = 9 };

            var plain = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm(), user, true, false);
            Assert.AreEqual(9, plain.DepartmentId, "existing behaviour is unchanged");

            var sectioned = ListRoleDefaultsHelper.ApplyAssetListDefaults(new AssetFilterVm { Section = DepartmentSection.Prep }, user, true, false);
            Assert.IsFalse(sectioned.DepartmentId.HasValue);
            Assert.IsNull(sectioned.CustodianUserId);
            Assert.AreEqual(DepartmentSection.Prep, sectioned.Section);
        }
    }
}
