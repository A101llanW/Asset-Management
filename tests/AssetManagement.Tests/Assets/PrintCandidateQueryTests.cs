using System;
using System.Data.SqlClient;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class PrintCandidateQueryTests
    {
        [Test]
        public void Parse_Defaults_TagAscTake50()
        {
            var q = PrintCandidateQuery.Parse(null, null, null, null, null, null, null);
            Assert.IsTrue(q.IsValid);
            Assert.AreEqual("tag", q.Sort);
            Assert.AreEqual("asc", q.Direction);
            Assert.AreEqual(50, q.Take);
        }

        [TestCase("created")]
        [TestCase("ASSIGNED")]
        [TestCase(" activity ")]
        public void Parse_DateSorts_DefaultNewestFirst(string sort)
        {
            var q = PrintCandidateQuery.Parse(sort, null, null, null, null, null, 10);
            Assert.IsTrue(q.IsValid, q.Error);
            Assert.AreEqual(sort.Trim().ToLowerInvariant(), q.Sort);
            Assert.AreEqual("desc", q.Direction);
        }

        [TestCase("name")]
        [TestCase("a.[CreatedAt]; DROP TABLE Asset")]
        [TestCase("bookvalue")]
        public void Parse_SortOutsideWhitelist_IsRejected(string sort)
        {
            var q = PrintCandidateQuery.Parse(sort, null, null, null, null, null, null);
            Assert.IsFalse(q.IsValid);
            StringAssert.Contains("Unknown sort", q.Error);
        }

        [Test]
        public void Parse_BadDirection_IsRejected()
        {
            Assert.IsFalse(PrintCandidateQuery.Parse("created", "sideways", null, null, null, null, null).IsValid);
            Assert.AreEqual("asc", PrintCandidateQuery.Parse("created", "ASC", null, null, null, null, null).Direction);
        }

        [TestCase("13/10/2026")]
        [TestCase("2026-13-01")]
        [TestCase("yesterday")]
        public void Parse_BadDate_IsRejected(string value)
        {
            Assert.IsFalse(PrintCandidateQuery.Parse(null, null, value, null, null, null, null).IsValid);
            Assert.IsFalse(PrintCandidateQuery.Parse(null, null, null, null, null, value, null).IsValid);
        }

        [Test]
        public void Parse_FromAfterTo_IsRejected()
        {
            Assert.IsFalse(PrintCandidateQuery.Parse(null, null, "2026-10-13", "2026-10-01", null, null, null).IsValid);
            Assert.IsFalse(PrintCandidateQuery.Parse(null, null, null, null, "2026-10-13", "2026-10-01", null).IsValid);
        }

        [Test]
        public void Parse_Dates_AreEatDaysAsUtcRange()
        {
            var q = PrintCandidateQuery.Parse("created", "desc", "2026-10-13", "2026-10-13", "2026-10-01", "2026-10-07", null);
            Assert.IsTrue(q.IsValid, q.Error);
            Assert.AreEqual(KenyaTimeHelper.StartOfLocalDayToUtc(new DateTime(2026, 10, 13)), q.CreatedFromUtc);
            Assert.AreEqual(KenyaTimeHelper.ExclusiveEndOfLocalDayToUtc(new DateTime(2026, 10, 13)), q.CreatedToUtcExclusive);
            Assert.AreEqual(TimeSpan.FromDays(1), q.CreatedToUtcExclusive.Value - q.CreatedFromUtc.Value);
            Assert.AreEqual(TimeSpan.FromDays(7), q.AssignedToUtcExclusive.Value - q.AssignedFromUtc.Value);
        }

        [TestCase(0, 1)]
        [TestCase(-5, 1)]
        [TestCase(500, 100)]
        [TestCase(25, 25)]
        public void Parse_Take_IsCapped(int take, int expected)
        {
            Assert.AreEqual(expected, PrintCandidateQuery.Parse(null, null, null, null, null, null, take).Take);
        }

        [Test]
        public void PrintCandidates_KeepsAssetsViewAuthorization()
        {
            var method = typeof(AssetScanController).GetMethod("PrintCandidates");
            var names = new System.Collections.Generic.List<string>();
            foreach (var a in method.GetCustomAttributesData())
            {
                names.Add(a.Constructor.DeclaringType.Name);
                if (a.Constructor.DeclaringType.Name == "PermissionAuthorizeAttribute")
                {
                    Assert.AreEqual("Assets.View", a.ConstructorArguments[0].Value);
                }
            }

            CollectionAssert.IsSubsetOf(new[] { "HttpGetAttribute", "AuthorizeAttribute", "TenantAuthorizeAttribute", "PermissionAuthorizeAttribute" }, names);
        }
    }

    [TestFixture]
    public class AssetQueryPrintCandidateSqlTests
    {
        [Test]
        public void WhereClause_NoDates_HasNoDateFilters()
        {
            var sql = AssetQueryService.BuildWhereClause(new AssetFilterVm());
            StringAssert.DoesNotContain("@CreatedFromUtc", sql);
            StringAssert.DoesNotContain("AssetAssignment", sql);
            StringAssert.Contains("a.[OrganizationId] = @OrganizationId", sql);
        }

        [Test]
        public void WhereClause_CreatedRange_FiltersCreatedAtAndKeepsOrgScope()
        {
            var sql = AssetQueryService.BuildWhereClause(new AssetFilterVm
            {
                CreatedFromUtc = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc),
                CreatedToUtcExclusive = new DateTime(2026, 10, 13, 21, 0, 0, DateTimeKind.Utc)
            });
            StringAssert.Contains("a.[CreatedAt] >= @CreatedFromUtc", sql);
            StringAssert.Contains("a.[CreatedAt] < @CreatedToUtc", sql);
            StringAssert.Contains("a.[OrganizationId] = @OrganizationId", sql);
            StringAssert.Contains("a.[IsActive] = 1", sql);
        }

        [Test]
        public void WhereClause_AssignedRange_UsesAssignmentExists()
        {
            var sql = AssetQueryService.BuildWhereClause(new AssetFilterVm { AssignedFromUtc = DateTime.UtcNow.AddDays(-7) });
            StringAssert.Contains("EXISTS (SELECT 1 FROM [AssetAssignment] aaf WHERE aaf.[AssetId] = a.[Id] AND aaf.[AssignedDate] >= @AssignedFromUtc)", sql);
            StringAssert.DoesNotContain("@AssignedToUtc", sql);
        }

        [Test]
        public void FilterParameters_IncludeDateValues()
        {
            var from = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc);
            using (var command = new SqlCommand())
            {
                AssetQueryService.AddFilterParameters(command, new AssetFilterVm { CreatedFromUtc = from });
                Assert.AreEqual(from, command.Parameters["@CreatedFromUtc"].Value);
                Assert.AreEqual(DBNull.Value, command.Parameters["@CreatedToUtc"].Value);
                Assert.AreEqual(DBNull.Value, command.Parameters["@AssignedFromUtc"].Value);
                Assert.AreEqual(DBNull.Value, command.Parameters["@AssignedToUtc"].Value);
            }
        }

        [Test]
        public void OrderBy_Created_BothDirections()
        {
            Assert.AreEqual("a.[CreatedAt] DESC, a.[Id] DESC", AssetQueryService.BuildOrderBy("created", "desc"));
            Assert.AreEqual("a.[CreatedAt] ASC, a.[Id] ASC", AssetQueryService.BuildOrderBy("created", "asc"));
        }

        [Test]
        public void OrderBy_Assigned_UsesLatestAssignmentWithNullsLast()
        {
            var desc = AssetQueryService.BuildOrderBy("assigned", "desc");
            var asc = AssetQueryService.BuildOrderBy("assigned", "asc");
            StringAssert.StartsWith("CASE WHEN " + AssetQueryService.LastAssignedSql + " IS NULL THEN 1 ELSE 0 END ASC", desc);
            StringAssert.EndsWith(AssetQueryService.LastAssignedSql + " DESC, a.[Id] DESC", desc);
            StringAssert.EndsWith(AssetQueryService.LastAssignedSql + " ASC, a.[Id] ASC", asc);
            StringAssert.Contains("MAX(aas.[AssignedDate])", AssetQueryService.LastAssignedSql);
        }

        [Test]
        public void OrderBy_Activity_UsesCustodyEventsAndAssetTimestamps()
        {
            var sql = AssetQueryService.BuildOrderBy("activity", "desc");
            StringAssert.EndsWith(" DESC, a.[Id] DESC", sql);
            StringAssert.Contains("[AssetCustodyEvent]", sql);
            StringAssert.Contains("a.[UpdatedAt]", sql);
            StringAssert.Contains("a.[CreatedAt]", sql);
        }
    }
}
