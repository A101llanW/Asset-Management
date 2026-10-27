using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>rel1027: the QR scan card ({org}/AssetScan/Lookup?code=TAG) and what each viewer may see.</summary>
    [TestFixture]
    public class AssetScanCardTests
    {
        private static AssetScanLookupVm SampleLookup()
        {
            return new AssetScanLookupVm
            {
                Found = true,
                AssetId = 42,
                AssetTag = "G7-G7B-045",
                AssetName = "Teacher desk",
                CategoryName = "Furniture",
                AssetTypeName = "Desk",
                AssetSubTypeName = "Teacher desk 120cm",
                Brand = "Victoria",
                Model = "TD-120",
                SerialNumber = "SN-1",
                CurrentStatus = AssetStatus.InStore,
                Condition = AssetCondition.RepairNeeded,
                PurchaseDate = new DateTime(2026, 1, 10),
                ReceivedDate = new DateTime(2026, 2, 3),
                DepartmentId = 9,
                DepartmentName = "Grade 7B",
                Section = DepartmentSection.SeniorSchool,
                OrganizationUsesPrep = true,
                CustodianEmail = "teacher@example.com",
                AcquisitionCost = 12500m,
                Currency = "kes",
                SupplierName = "Acme Furniture Ltd",
                PhotoFilePath = "uploads/assets/42/photo.jpg",
                PhotoContentType = "image/jpeg",
                PhotoDocumentType = "Asset photo",
                LocationPath = new List<AssetScanLocationLevel>
                {
                    new AssetScanLocationLevel { Name = "Academics", Kind = DepartmentKind.Administrative },
                    new AssetScanLocationLevel { Name = "Grade 7", Kind = DepartmentKind.Grade },
                    new AssetScanLocationLevel { Name = "Grade 7B", Kind = DepartmentKind.Class },
                    new AssetScanLocationLevel { Name = "Room 12", Kind = DepartmentKind.Room }
                }
            };
        }

        private static string Row(AssetScanCardVm card, string key)
        {
            var row = card.Rows.Concat(card.RestrictedRows).FirstOrDefault(x => x.Key == key);
            return row == null ? null : row.Value;
        }

        private static string AllText(AssetScanCardVm card)
        {
            return string.Join("|", card.Rows.Concat(card.RestrictedRows).Select(x => x.Label + "=" + x.Value).ToArray())
                + "|" + card.AssetName + "|" + card.AssetTag + "|" + card.CustodianText;
        }

        [Test]
        public void Anonymous_SeesUsefulDetails()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Anonymous);

            Assert.AreEqual("Teacher desk", card.AssetName);
            Assert.AreEqual("G7-G7B-045", card.AssetTag);
            Assert.AreEqual("Furniture \u203A Desk \u203A Teacher desk 120cm", Row(card, "category"));
            Assert.AreEqual("Victoria / TD-120", Row(card, "brand-model"));
            Assert.IsNull(Row(card, "serial"), "review S8: serial hidden from signed-out viewers");
            Assert.AreEqual("Academics", Row(card, "location-administrative"));
            Assert.AreEqual("Year 7", Row(card, "location-grade"));
            Assert.AreEqual("Year 7B", Row(card, "location-class"));
            Assert.AreEqual("Room 12", Row(card, "location-room"));
            Assert.AreEqual("Senior school", Row(card, "section"));
            Assert.AreEqual("In store", Row(card, "status"));
            Assert.AreEqual("Repair needed", Row(card, "condition"));
            Assert.AreEqual("3 Feb 2026", Row(card, "received"), "receiving date wins over the purchase date");
            Assert.IsTrue(card.HasPhoto);
        }

        [Test]
        public void LocationLabels_UseYearAndClassroomWording()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Anonymous);
            var labels = card.Rows.Select(x => x.Label).ToList();
            CollectionAssert.Contains(labels, "Year");
            CollectionAssert.Contains(labels, "Classroom");
            CollectionAssert.Contains(labels, "Room");
            CollectionAssert.DoesNotContain(labels, "Grade");
            CollectionAssert.DoesNotContain(labels, "Class");
            Assert.AreEqual("G7-G7B-045", card.AssetTag, "the tag is shown exactly as stored");
        }

        [Test]
        public void Anonymous_NeverSeesEmailCostSupplier()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            var card = AssetScanCardBuilder.Build(lookup, AssetScanViewer.Anonymous);
            var text = AllText(card);

            Assert.IsFalse(text.Contains("@"), text);
            Assert.IsFalse(text.Contains("12,500"), text);
            Assert.IsFalse(text.Contains("Acme"), text);
            Assert.AreEqual(0, card.RestrictedRows.Count);
            Assert.AreEqual("In Room 12", card.CustodianText, "signed out: location only");
        }

        [Test]
        public void Anonymous_NeverSeesCustodianName()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            lookup.CustodianPersonName = "Jane Wanjiru";
            var card = AssetScanCardBuilder.Build(lookup, AssetScanViewer.Anonymous);

            StringAssert.DoesNotContain("Jane", AllText(card));
            Assert.AreEqual("In Room 12", card.CustodianText);
            Assert.IsNull(Row(card, "custodian"), "no Custodian row for signed-out viewers");
            Assert.AreEqual("In Room 12", Row(card, "where"));
        }

        [Test]
        public void Anonymous_AssignedButNoUnit_NoName()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            lookup.CustodianPersonName = "Jane Wanjiru";
            lookup.LocationPath.Clear();
            lookup.DepartmentName = null;
            Assert.AreEqual(AssetScanCardBuilder.SignInToSeeCustodianText, AssetScanCardBuilder.CustodianText(lookup, AssetScanViewer.Anonymous));
        }

        [Test]
        public void SignedIn_NoName_NoAssetsView_DoesNotShowEmail()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            Assert.AreEqual(AssetScanCardBuilder.AssignedNoNameText,
                AssetScanCardBuilder.CustodianText(lookup, new AssetScanViewer { IsSignedIn = true }));
        }

        [Test]
        public void SignedInWithoutPermissions_NoFinancialsOrSupplier()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), new AssetScanViewer { IsSignedIn = true });
            Assert.AreEqual(0, card.RestrictedRows.Count);
        }

        [Test]
        public void SignedInWithPermissions_SeesCostAndSupplier()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), new AssetScanViewer
            {
                IsSignedIn = true,
                CanViewAssetDetails = true,
                CanViewFinancials = true,
                CanViewSuppliers = true
            });

            Assert.AreEqual("KES 12,500.00", Row(card, "cost"));
            Assert.AreEqual("Acme Furniture Ltd", Row(card, "supplier"));
        }

        [Test]
        public void PermissionFlagsWithoutSignIn_AreIgnored()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), new AssetScanViewer { CanViewFinancials = true, CanViewSuppliers = true });
            Assert.AreEqual(0, card.RestrictedRows.Count);
        }

        [Test]
        public void Custodian_SignedIn_NamedPersonShown()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            lookup.CustodianPersonName = "Jane Wanjiru";
            var card = AssetScanCardBuilder.Build(lookup, new AssetScanViewer { IsSignedIn = true });
            Assert.AreEqual("Jane Wanjiru", card.CustodianText);
            Assert.AreEqual("Jane Wanjiru", Row(card, "custodian"));
        }

        [Test]
        public void Custodian_NoNameSignedInWithView_ShowsEmail()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u1";
            var viewer = new AssetScanViewer { IsSignedIn = true, CanViewAssetDetails = true };
            Assert.AreEqual("teacher@example.com", AssetScanCardBuilder.CustodianText(lookup, viewer));
        }

        [Test]
        public void Custodian_Unassigned_ShowsInDeepestUnit()
        {
            var card = AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Anonymous);
            Assert.AreEqual("In Room 12", card.CustodianText);
            Assert.AreNotEqual("Company custody", card.CustodianText);
        }

        [Test]
        public void Custodian_UnassignedInClassroom_UsesYearWording()
        {
            var lookup = SampleLookup();
            lookup.LocationPath.RemoveAt(3);
            Assert.AreEqual("In Year 7B", AssetScanCardBuilder.Build(lookup, AssetScanViewer.Anonymous).CustodianText);
        }

        [Test]
        public void Custodian_NoUnitAtAll_FallsBackToCompanyCustody()
        {
            var lookup = SampleLookup();
            lookup.LocationPath.Clear();
            lookup.DepartmentName = null;
            lookup.DepartmentId = null;
            var card = AssetScanCardBuilder.Build(lookup, AssetScanViewer.Anonymous);
            Assert.AreEqual(AssetScanCardBuilder.CompanyCustodyText, card.CustodianText);
            Assert.IsNull(Row(card, "section"));
        }

        [Test]
        public void Section_PrepShown_AndHiddenWhenOrganisationHasNoPrep()
        {
            var lookup = SampleLookup();
            lookup.Section = DepartmentSection.Prep;
            Assert.AreEqual("Prep", Row(AssetScanCardBuilder.Build(lookup, null), "section"));

            lookup.OrganizationUsesPrep = false;
            Assert.IsNull(Row(AssetScanCardBuilder.Build(lookup, null), "section"));
        }

        [Test]
        public void OptionalFields_SkippedWhenBlank()
        {
            var lookup = SampleLookup();
            lookup.Brand = null;
            lookup.Model = " ";
            lookup.SerialNumber = null;
            lookup.AssetSubTypeName = null;
            lookup.ReceivedDate = null;
            lookup.PurchaseDate = DateTime.MinValue;
            lookup.PhotoFilePath = null;
            var card = AssetScanCardBuilder.Build(lookup, null);
            Assert.IsNull(Row(card, "brand-model"));
            Assert.IsNull(Row(card, "serial"));
            Assert.IsNull(Row(card, "received"));
            Assert.AreEqual("Furniture \u203A Desk", Row(card, "category"));
            Assert.IsFalse(card.HasPhoto);
        }

        [Test]
        public void NotFound_NoCard()
        {
            Assert.IsNull(AssetScanCardBuilder.Build(new AssetScanLookupVm { Found = false }, null));
            Assert.IsNull(AssetScanCardBuilder.Build(null, null));
        }

        [TestCase("image/jpeg", true)]
        [TestCase("IMAGE/PNG", true)]
        [TestCase("image/gif", true)]
        [TestCase("application/pdf", false)]
        [TestCase("image/svg+xml", false)]
        [TestCase("text/html", false)]
        [TestCase(null, false)]
        public void PhotoEndpoint_OnlyServesRasterImages(string contentType, bool allowed)
        {
            Assert.AreEqual(allowed, AssetScanPhotoRules.IsAllowedContentType(contentType));
        }

        [Test]
        public void ServiceLookup_PublicMode_ReturnsDetailsWithoutDepartmentScope()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 7,
                AssetTag = "G7-G7B-045",
                AssetName = "Teacher desk",
                OrganizationId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                DepartmentId = 1,
                Currency = "KES",
                AcquisitionCost = 500,
                CurrentStatus = AssetStatus.Assigned,
                Condition = AssetCondition.Good,
                PurchaseDate = new DateTime(2026, 3, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            var result = service.LookupByScanCode("G7-G7B-045", applyDepartmentScope: false, includeDetails: true);

            Assert.IsTrue(result.Found);
            Assert.AreEqual("Teacher desk", result.AssetName);
            Assert.AreEqual(AssetCondition.Good, result.Condition);
            var card = AssetScanCardBuilder.Build(result, AssetScanViewer.Anonymous);
            Assert.AreEqual("Good", Row(card, "condition"));
            Assert.AreEqual(0, card.RestrictedRows.Count);
        }

        // ---- the QR payload must not change: already-printed labels keep working ----

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AssetManagementModule.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "repository root not found");
            return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray()));
        }

        [Test]
        public void QrUrlFormat_IsUnchanged()
        {
            var helper = RepoFile("src", "AssetManagement.Web", "Helpers", "AssetQrUrlHelper.cs");
            StringAssert.Contains("TenantUrlHelper.BuildTenantPath(tenant, \"AssetScan\", \"Lookup\")", helper);
            StringAssert.Contains("+ \"?code=\" + HttpUtility.UrlEncode(assetTag.Trim())", helper);
        }

        [Test]
        public void LookupView_ScannedAssetDetailsComeBeforeTheSearchPanel()
        {
            var view = RepoFile("src", "AssetManagement.Web", "Views", "AssetScan", "Lookup.cshtml");
            var detailsFirst = view.IndexOf("data-am-scan-order=\"details-first\"", StringComparison.Ordinal);
            var panel = view.IndexOf("data-am-scan-panel=\"1\"", StringComparison.Ordinal);
            Assert.Greater(detailsFirst, 0);
            Assert.Greater(panel, detailsFirst, "a found asset renders above the lookup panel");
            StringAssert.Contains("detailsFirst: @(scanResultFirst", view);
            StringAssert.Contains("var scanAutofocusAttr = scanResultFirst ? \"\" : ", view, "no autofocus attribute at all (no jump to the search box) when details are first");
        }

        [Test]
        public void LookupAction_StaysPublic_AndPhotoIsImageOnly()
        {
            var controller = RepoFile("src", "AssetManagement.Web", "Controllers", "AssetScanController.cs");
            var lookupIndex = controller.IndexOf("public ActionResult Lookup(string code, string q)", StringComparison.Ordinal);
            Assert.Greater(lookupIndex, 0);
            var before = controller.Substring(Math.Max(0, lookupIndex - 200), 200);
            StringAssert.DoesNotContain("[Authorize]", before, "the QR target must stay reachable without login");
            StringAssert.Contains("AssetScanPhotoRules.IsAllowedContentType", controller);
        }

        // ---- review fixes S6 / S7 / S8 ----

        [Test]
        public void S6_SignedInWithoutAssetsView_GetsThePublicCard_NoCustodianCostOrSupplier()
        {
            var lookup = SampleLookup();
            lookup.CustodianUserId = "u-1";
            lookup.CustodianPersonName = "Jane Teacher";
            var viewer = AssetScanViewer.Resolve(isAuthenticated: true, canViewDetails: false, outsideScope: false, canViewFinancials: true, canViewSuppliers: true, showSerialToPublic: false);

            Assert.IsFalse(viewer.IsSignedIn);
            Assert.IsFalse(viewer.CanViewAssetDetails);
            var card = AssetScanCardBuilder.Build(lookup, viewer);
            var text = AllText(card);
            StringAssert.DoesNotContain("Jane Teacher", text);
            StringAssert.DoesNotContain("teacher@example.com", text);
            StringAssert.DoesNotContain("12,500", text);
            StringAssert.DoesNotContain("Acme", text);
            Assert.IsNull(Row(card, "custodian"));
            Assert.AreEqual("In Room 12", Row(card, "where"));
        }

        [Test]
        public void S6_Resolve_OnlyAssetsViewInScopeIsSignedIn()
        {
            Assert.IsFalse(AssetScanViewer.Resolve(false, false, false, true, true, false).IsSignedIn, "signed out");
            Assert.IsFalse(AssetScanViewer.Resolve(true, true, true, true, true, false).IsSignedIn, "outside scope");
            var full = AssetScanViewer.Resolve(true, true, false, true, false, false);
            Assert.IsTrue(full.IsSignedIn);
            Assert.IsTrue(full.CanViewAssetDetails);
            Assert.IsTrue(full.CanViewFinancials);
            Assert.IsFalse(full.CanViewSuppliers);

            var lookup = SampleLookup();
            lookup.CustodianUserId = "u-1";
            lookup.CustodianPersonName = "Jane Teacher";
            var card = AssetScanCardBuilder.Build(lookup, full);
            Assert.AreEqual("Jane Teacher", Row(card, "custodian"));
            StringAssert.Contains("12,500", AllText(card));
        }

        [TestCase("Asset photo", false, true)]
        [TestCase("Asset photo 2", false, true)]
        [TestCase("Serial plate photo", false, false)]
        [TestCase("Location photo", false, false)]
        [TestCase(null, false, false)]
        [TestCase("Serial plate photo", true, true)]
        [TestCase("Location photo", true, true)]
        public void S7_PhotoType_PublicOnlyAssetPhoto(string documentType, bool canViewDetails, bool expected)
        {
            Assert.AreEqual(expected, AssetScanPhotoRules.MayServe(documentType, canViewDetails));
            var lookup = SampleLookup();
            lookup.PhotoDocumentType = documentType;
            var viewer = canViewDetails ? AssetScanViewer.Resolve(true, true, false, false, false, false) : AssetScanViewer.Anonymous;
            Assert.AreEqual(expected, AssetScanCardBuilder.Build(lookup, viewer).HasPhoto);
        }

        [Test]
        public void S7_PhotoAction_ChecksTheDocumentType()
        {
            var controller = RepoFile("src", "AssetManagement.Web", "Controllers", "AssetScanController.cs");
            StringAssert.Contains("AssetScanPhotoRules.MayServe(lookup.PhotoDocumentType, mayViewAnyPhotoType)", controller);
            var repo = RepoFile("src", "AssetManagement.Infrastructure", "Repositories", "AssetScanLookupRepository.cs");
            StringAssert.Contains("ph.[DocumentType] AS PhotoDocumentType", repo);
        }

        [Test]
        public void S8_Serial_HiddenFromPublicByDefault_SwitchShowsIt()
        {
            Assert.IsNull(Row(AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Anonymous), "serial"), "signed out: hidden");
            Assert.IsNull(Row(AssetScanCardBuilder.Build(SampleLookup(), null), "serial"), "no viewer: hidden");
            Assert.IsNull(Row(AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Resolve(true, false, false, false, false, false)), "serial"), "signed in without Assets.View: hidden");
            Assert.IsNull(Row(AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Resolve(true, true, true, false, false, false)), "serial"), "outside scope: hidden");
            Assert.AreEqual("SN-1", Row(AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Resolve(true, true, false, false, false, false)), "serial"), "Assets.View: shown");
            Assert.AreEqual("SN-1", Row(AssetScanCardBuilder.Build(SampleLookup(), AssetScanViewer.Resolve(false, false, false, false, false, true)), "serial"), "switch on: shown");
            var controller = RepoFile("src", "AssetManagement.Web", "Controllers", "AssetScanController.cs");
            StringAssert.Contains("\"AssetScan:ShowSerialToPublic\"", controller);
            StringAssert.Contains("matchSerialNumber: canViewDetails || ShowSerialToPublic()", controller);
        }

        [Test]
        public void S8_LookupBySerial_OnlyWhenSerialMatchingIsAllowed()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 8, AssetTag = "G7-G7B-046", AssetName = "Projector", SerialNumber = "PJ-998877", OrganizationId = 1,
                CategoryId = 1, AssetTypeId = 1, DepartmentId = 1, Currency = "KES", AcquisitionCost = 500,
                CurrentStatus = AssetStatus.InStore, Condition = AssetCondition.Good, PurchaseDate = new DateTime(2026, 3, 1),
                DepreciationMethod = DepreciationMethod.StraightLine, DepreciationStartDate = DateTime.UtcNow, UsefulLifeMonths = 36,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            var service = TestServiceFactory.CreateAssetService(unitOfWork);

            Assert.IsTrue(service.LookupByScanCode("PJ-998877", applyDepartmentScope: false, includeDetails: true).Found, "Assets.View: serial lookup works");
            Assert.IsFalse(service.LookupByScanCode("PJ-998877", applyDepartmentScope: false, includeDetails: true, matchSerialNumber: false).Found, "public: no serial lookup");
            Assert.IsTrue(service.LookupByScanCode("G7-G7B-046", applyDepartmentScope: false, includeDetails: true, matchSerialNumber: false).Found, "public: tag still works");
        }

        [Test]
        public void SignedInOutsideScope_GetsThePublicCardOnly()
        {
            var controller = RepoFile("src", "AssetManagement.Web", "Controllers", "AssetScanController.cs");
            StringAssert.Contains("AssetScanViewer.Resolve(", controller, "signed-out, no-Assets.View and out-of-scope viewers get the anonymous card (no names, cost, supplier)");
            StringAssert.Contains("CanOpenQuickActions = lookup.Found && !outsideScope", controller);
            StringAssert.Contains("page.IsPublicScan || page.OutsideYourDepartments", controller, "JSON for out-of-scope viewers is the public shape");
        }
    }
}
