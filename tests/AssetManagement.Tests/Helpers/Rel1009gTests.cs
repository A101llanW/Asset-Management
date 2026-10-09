using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Web.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>rel1009g: URL builders (item 6).</summary>
    [TestFixture]
    public class Rel1009gUrlTests
    {
        private static string Base(string configured, string request, string appPath)
        {
            var r = AssetScanUrlHelper.ResolveAbsoluteBase(configured, request == null ? null : new Uri(request), appPath);
            return r == null ? null : r.TrimEnd('/');
        }

        [Test]
        public void ConfiguredPublicBase_WithSubfolder_IsUsed()
        {
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite",
                Base("https://nanosoft.africa/NanoAssetSuite/", "http://localhost:8080/NanoAssetSuite/Assets", "/NanoAssetSuite"));
        }

        [Test]
        public void HostOnlyConfiguredBase_GetsApplicationPathAppended()
        {
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite",
                Base("https://nanosoft.africa", "https://nanosoft.africa/NanoAssetSuite/Assets", "/NanoAssetSuite"));
        }

        [Test]
        public void LocalhostConfig_WithPublicRequest_FallsBackToRequestPlusAppPath()
        {
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite",
                Base("http://localhost/NanoAssetSuite", "https://nanosoft.africa/NanoAssetSuite/AssetRequests/Details/5", "/NanoAssetSuite"));
        }

        [Test]
        public void LocalhostConfig_WithLocalRequestOnSamePort_KeepsConfig()
        {
            Assert.AreEqual("http://localhost/NanoAssetSuite",
                Base("http://localhost/NanoAssetSuite", "http://localhost/NanoAssetSuite/Assets", "/NanoAssetSuite"));
        }

        [Test]
        public void NoConfig_UsesRequestAuthorityPlusAppPath_WithPort()
        {
            Assert.AreEqual("http://server:8080/NanoAssetSuite",
                Base(null, "http://server:8080/NanoAssetSuite/Users", "/NanoAssetSuite"));
        }

        [Test]
        public void RootApplication_HasNoTrailingSegment()
        {
            Assert.AreEqual("https://assets.example.org",
                Base(null, "https://assets.example.org/Assets", "/"));
        }

        [Test]
        public void ExternalUrlHelper_ResolveBaseUri_SubfolderAndOrgCode()
        {
            var uri = ExternalUrlHelper.ResolveBaseUri(new Uri("https://nanosoft.africa/NanoAssetSuite/Platform/Organizations"), "/NanoAssetSuite", "http://localhost/NanoAssetSuite");
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite/", uri.ToString());
            var portal = uri.ToString().TrimEnd('/') + "/" + "A461" + "/Account/Login";
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite/A461/Account/Login", portal);
        }

        [Test]
        public void ExternalUrlHelper_ResolveBaseUri_NoRequestNoConfig_FallsBackToLocalhost()
        {
            Assert.AreEqual("http://localhost/", ExternalUrlHelper.ResolveBaseUri(null, null, null).ToString());
        }

        [Test]
        public void PasswordReset_LocalhostConfig_PublicRequest_UsesRequest()
        {
            var r = AssetScanUrlHelper.ResolvePasswordResetBaseUrl("http://localhost", new Uri("https://nanosoft.africa/NanoAssetSuite/Account/ForgotPassword"), "/NanoAssetSuite");
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite", r.TrimEnd('/'));
        }

        [Test]
        public void CombineBaseAndRelative_JoinsWithSingleSlash()
        {
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite/AssetScan/Lookup",
                AssetScanUrlHelper.CombineBaseAndRelative("https://nanosoft.africa/NanoAssetSuite/", "/AssetScan/Lookup"));
        }

        [TestCase("http://localhost/NanoAssetSuite", true)]
        [TestCase("http://127.0.0.1:8080/x", true)]
        [TestCase("https://nanosoft.africa/NanoAssetSuite", false)]
        [TestCase("", false)]
        public void IsLoopbackUrl(string url, bool expected)
        {
            Assert.AreEqual(expected, AssetScanUrlHelper.IsLoopbackUrl(url));
        }

        [Test]
        public void Validator_WarnsOnLocalhost()
        {
            StringAssert.Contains("localhost", ExternalBaseUrlValidator.Validate("http://localhost/NanoAssetSuite", "/NanoAssetSuite"));
        }

        [Test]
        public void Validator_WarnsOnWrongAppPath()
        {
            StringAssert.Contains("/NanoAssetSuite", ExternalBaseUrlValidator.Validate("https://nanosoft.africa/nanosoft", "/NanoAssetSuite"));
        }

        [Test]
        public void Validator_WarnsOnMisspelledAppPath()
        {
            Assert.IsNotNull(ExternalBaseUrlValidator.Validate("https://nanosoft.africa/NanoAssetSuit", "/NanoAssetSuite"));
        }

        [Test]
        public void Validator_WarnsOnHostOnly_WhenAppIsInSubfolder()
        {
            StringAssert.Contains("appended", ExternalBaseUrlValidator.Validate("https://nanosoft.africa", "/NanoAssetSuite"));
        }

        [Test]
        public void Validator_WarnsOnInvalid()
        {
            Assert.IsNotNull(ExternalBaseUrlValidator.Validate("nanosoft.africa/NanoAssetSuite", "/NanoAssetSuite"));
        }

        [TestCase("https://nanosoft.africa/NanoAssetSuite", "/NanoAssetSuite")]
        [TestCase("https://nanosoft.africa/nanoassetsuite/", "/NanoAssetSuite")]
        [TestCase("https://assets.example.org", "/")]
        public void Validator_AcceptsGoodValues(string url, string appPath)
        {
            Assert.IsNull(ExternalBaseUrlValidator.Validate(url, appPath));
        }
    }

    /// <summary>rel1009g: requisition tree / room targets (item 3).</summary>
    [TestFixture]
    public class Rel1009gDepartmentTreeTests
    {
        private static List<DepartmentVm> Tree()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Grade 2", Code = "G2", DepartmentKind = DepartmentKind.Grade, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 2, Name = "Grade 1", Code = "G1", DepartmentKind = DepartmentKind.Grade, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 3, Name = "1 East", Code = "G1E", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class, IsActive = true, IsRequisitionTarget = true, ClassTeacherUserId = "teacher" },
                new DepartmentVm { Id = 4, Name = "Lab A", Code = "G1EL", ParentDepartmentId = 3, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true, RoomCustodianUserId = " custodian " },
                new DepartmentVm { Id = 5, Name = "ICT", Code = "ICT", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 6, Name = "Old", Code = "OLD", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class, IsActive = false, IsRequisitionTarget = true }
            };
        }

        [Test]
        public void BuildPathLabel_FullDepth()
        {
            Assert.AreEqual("Grade 1 > 1 East > Lab A", DepartmentTreeHelper.BuildPathLabel(4, Tree()));
        }

        [Test]
        public void GetSelfAndDescendantIds_IncludesAllDepths()
        {
            CollectionAssert.IsSubsetOf(new[] { 2, 3, 4 }, DepartmentTreeHelper.GetSelfAndDescendantIds(2, Tree()).ToList());
            CollectionAssert.DoesNotContain(DepartmentTreeHelper.GetSelfAndDescendantIds(2, Tree()).ToList(), 1);
        }

        [Test]
        public void IsSelfOrDescendant_WalksParents()
        {
            var parents = Tree().ToDictionary(x => x.Id, x => x.ParentDepartmentId);
            Assert.IsTrue(DepartmentTreeHelper.IsSelfOrDescendant(4, 2, parents));
            Assert.IsTrue(DepartmentTreeHelper.IsSelfOrDescendant(2, 2, parents));
            Assert.IsFalse(DepartmentTreeHelper.IsSelfOrDescendant(4, 1, parents));
            Assert.IsFalse(DepartmentTreeHelper.IsSelfOrDescendant(null, 1, parents));
        }

        [Test]
        public void IsSelfOrDescendant_SurvivesCycles()
        {
            var parents = new Dictionary<int, int?> { { 1, 2 }, { 2, 1 } };
            Assert.IsFalse(DepartmentTreeHelper.IsSelfOrDescendant(1, 3, parents));
        }

        [Test]
        public void BuildRequisitionOptions_GradesFirstByCode_ThenAdmin_TreeOrder_SkipsInactive()
        {
            var options = DepartmentTreeHelper.BuildRequisitionOptions(Tree());
            var ids = options.Select(x => x.Key).ToList();
            CollectionAssert.AreEqual(new[] { 2, 3, 4, 1, 5 }, ids);
            Assert.AreEqual("Grade 1 > 1 East > Lab A", options.Single(x => x.Key == 4).Value);
        }

        [Test]
        public void FormatAssetOption_TagSerialNameDept()
        {
            Assert.AreEqual("AT-1 - SN9 - Laptop (ICT)", DepartmentTreeHelper.FormatAssetOption("AT-1", "SN9", "Laptop", "ICT"));
            Assert.AreEqual("AT-1 - Laptop", DepartmentTreeHelper.FormatAssetOption("AT-1", " ", "Laptop", null));
        }

        [Test]
        public void ResolvePicUserId_ClassAndRoom()
        {
            var t = Tree();
            Assert.AreEqual("teacher", DepartmentTreeHelper.ResolvePicUserId(t[2]));
            Assert.AreEqual("custodian", DepartmentTreeHelper.ResolvePicUserId(t[3]));
            Assert.IsNull(DepartmentTreeHelper.ResolvePicUserId(t[0]));
            Assert.IsTrue(DepartmentTreeHelper.IsRoomTargetKind(DepartmentKind.Room));
            Assert.IsFalse(DepartmentTreeHelper.IsRoomTargetKind(DepartmentKind.Grade));
        }
    }

    /// <summary>rel1009g: deactivate / delete guards (item 5).</summary>
    [TestFixture]
    public class Rel1009gUserLifecycleTests
    {
        [Test]
        public void Rules_BlockSelf()
        {
            Assert.AreEqual(UserLifecycleRules.SelfMessage, UserLifecycleRules.Evaluate(UserLifecycleAction.Deactivate, "u1", "U1", true, false, 0));
            Assert.AreEqual(UserLifecycleRules.SelfMessage, UserLifecycleRules.Evaluate(UserLifecycleAction.Delete, "u1", "u1", false, false, 0));
        }

        [Test]
        public void Rules_BlockLastActiveCompanyAdmin()
        {
            Assert.AreEqual(UserLifecycleRules.LastAdminMessage, UserLifecycleRules.Evaluate(UserLifecycleAction.Deactivate, "a", "b", true, true, 0));
            Assert.IsNull(UserLifecycleRules.Evaluate(UserLifecycleAction.Deactivate, "a", "b", true, true, 1));
            Assert.IsNull(UserLifecycleRules.Evaluate(UserLifecycleAction.Delete, "a", "b", false, true, 0));
            Assert.IsNull(UserLifecycleRules.Evaluate(UserLifecycleAction.Reactivate, "a", "a", false, true, 0));
        }

        [Test]
        public void BlockedMessage_ListsCountsAndSuggestsDeactivation()
        {
            Assert.IsNull(UserLifecycleRules.BuildBlockedMessage(new[] { new KeyValuePair<string, int>("audit log entries", 0) }));
            var msg = UserLifecycleRules.BuildBlockedMessage(new[] { new KeyValuePair<string, int>("audit log entries", 12), new KeyValuePair<string, int>("assets in custody", 2) });
            StringAssert.Contains("12 audit log entries", msg);
            StringAssert.Contains("2 assets in custody", msg);
            StringAssert.Contains("Deactivate", msg);
        }

        private static UserService Service(ApplicationUser target, Mock<IUserLifecycleStore> store, Mock<IAuditWriter> audit, string actor = "admin-1")
        {
            var writer = new Mock<IEntityWriter<ApplicationUser>>();
            writer.Setup(x => x.GetById(target.Id)).Returns(target);
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(x => x.Writer<ApplicationUser>()).Returns(writer.Object);
            var scope = new Mock<IOrganizationScopeService>();
            scope.Setup(x => x.GetCurrentOrganizationId()).Returns(13);
            scope.Setup(x => x.IsCompanyAdmin()).Returns(true);
            return new UserService(uow.Object, Mock.Of<IAuthorizationService>(), scope.Object, new FakeCurrentUserContext(actor),
                audit.Object, Mock.Of<IUserAccountQueryRepository>(), Mock.Of<IReferenceDataCache>(), store.Object);
        }

        [Test]
        public void SetActive_False_RotatesTokenAndAudits()
        {
            var user = new ApplicationUser { Id = "t1", OrganizationId = 13, IsActive = true, AccessToken = "old" };
            var store = new Mock<IUserLifecycleStore>();
            var audit = new Mock<IAuditWriter>();
            Service(user, store, audit).SetActive("t1", false);
            Assert.IsFalse(user.IsActive);
            Assert.AreNotEqual("old", user.AccessToken);
            audit.Verify(x => x.Write("Users.Deactivate", It.IsAny<string>(), "t1", "Active", "Inactive"), Times.Once);
        }

        [Test]
        public void SetActive_OtherOrganization_IsNotFound()
        {
            var user = new ApplicationUser { Id = "t1", OrganizationId = 2, IsActive = true };
            Assert.Throws<BusinessException>(() => Service(user, new Mock<IUserLifecycleStore>(), new Mock<IAuditWriter>()).SetActive("t1", false));
        }

        [Test]
        public void SetActive_LastCompanyAdmin_Throws()
        {
            var user = new ApplicationUser { Id = "t1", OrganizationId = 13, IsActive = true };
            var store = new Mock<IUserLifecycleStore>();
            store.Setup(x => x.IsCompanyAdmin("t1", 13)).Returns(true);
            store.Setup(x => x.CountOtherActiveCompanyAdmins(13, "t1")).Returns(0);
            var ex = Assert.Throws<BusinessException>(() => Service(user, store, new Mock<IAuditWriter>()).SetActive("t1", false));
            Assert.AreEqual(UserLifecycleRules.LastAdminMessage, ex.Message);
            Assert.IsTrue(user.IsActive);
        }

        [Test]
        public void Delete_Self_Throws()
        {
            var user = new ApplicationUser { Id = "admin-1", OrganizationId = 13, IsActive = true };
            var store = new Mock<IUserLifecycleStore>();
            Assert.Throws<BusinessException>(() => Service(user, store, new Mock<IAuditWriter>()).Delete("admin-1"));
            store.Verify(x => x.HardDelete(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void Delete_WithHistory_IsBlocked()
        {
            var user = new ApplicationUser { Id = "t1", OrganizationId = 13, IsActive = true };
            var store = new Mock<IUserLifecycleStore>();
            store.Setup(x => x.GetDeleteBlockers("t1", 13)).Returns(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("audit log entries", 3) });
            var ex = Assert.Throws<BusinessException>(() => Service(user, store, new Mock<IAuditWriter>()).Delete("t1"));
            StringAssert.Contains("Deactivate", ex.Message);
            store.Verify(x => x.HardDelete(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void Delete_Clean_HardDeletesAndAudits()
        {
            var user = new ApplicationUser { Id = "t1", OrganizationId = 13, IsActive = true, Email = "x@y.invalid" };
            var store = new Mock<IUserLifecycleStore>();
            store.Setup(x => x.GetDeleteBlockers("t1", 13)).Returns(new List<KeyValuePair<string, int>>());
            var audit = new Mock<IAuditWriter>();
            Service(user, store, audit).Delete("t1");
            store.Verify(x => x.HardDelete("t1", 13), Times.Once);
            audit.Verify(x => x.Write("Users.Delete", It.IsAny<string>(), "t1", It.IsAny<string>(), null), Times.Once);
        }
    }

    /// <summary>rel1009g: audit log filters, label clamps (items 1-2).</summary>
    [TestFixture]
    public class Rel1009gAuditAndLabelTests
    {
        [Test]
        public void ActionFilter_IgnoresRouteAndUsesQuery()
        {
            Assert.IsNull(AuditLogFilterHelper.ResolveActionFilter(null, null));
            Assert.AreEqual("Users.Create", AuditLogFilterHelper.ResolveActionFilter(" Users.Create ", "Index"));
            Assert.AreEqual("Assets.Edit", AuditLogFilterHelper.ResolveActionFilter("", "Assets.Edit"));
        }

        [TestCase(0, 50)]
        [TestCase(3, 10)]
        [TestCase(25, 25)]
        [TestCase(5000, 200)]
        public void PageSize_Clamped(int input, int expected)
        {
            Assert.AreEqual(expected, AuditLogFilterHelper.NormalizePageSize(input));
            Assert.AreEqual(1, AuditLogFilterHelper.NormalizePage(-4));
        }

        [Test]
        public void PageView_And_Actor()
        {
            Assert.IsTrue(AuditLogFilterHelper.IsPageView("HTTP.Assets.Index", "VIEW:/Assets"));
            Assert.IsFalse(AuditLogFilterHelper.IsPageView("Assets.Edit", "VIEW:x"));
            Assert.AreEqual("Ann B (a@b.c)", AuditLogFilterHelper.FormatActor("id", "Ann", "B", "a@b.c"));
            Assert.AreEqual("System", AuditLogFilterHelper.FormatActor(null, null, null, null));
        }

        [Test]
        public void AuditWrites_AreSynchronousByDefault()
        {
            Assert.IsTrue(AuditWriter.ShouldWriteAuditSynchronously());
        }

        [Test]
        public void LabelNormalize_KeepsQrInsideLabel()
        {
            var design = LabelLayoutDesignHelper.CreateDefault(50, 25);
            var qr = LabelLayoutDesignHelper.GetElement(design, LabelLayoutDesignHelper.ScanCodeId);
            qr.XMm = 999;
            qr.YMm = -40;
            var normalized = LabelLayoutDesignHelper.Normalize(design, 50, 25, 3m);
            var nqr = LabelLayoutDesignHelper.GetElement(normalized, qr.Id);
            double w, h;
            LabelLayoutDesignHelper.GetElementFootprintMm(nqr, 3m, out w, out h);
            Assert.GreaterOrEqual(nqr.XMm, 0d);
            Assert.GreaterOrEqual(nqr.YMm, 0d);
            Assert.LessOrEqual(nqr.XMm + w, 50.0001d);
            Assert.LessOrEqual(nqr.YMm + h, 25.0001d);
        }
    }
}
