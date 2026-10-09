using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>rel1009i: QR cap (1), deactivated login (2), platform admin bootstrap (3), PO from requisition (4), receiving (5).</summary>
    [TestFixture]
    public class Rel1009iTests
    {
        // ---------------- item 1: QR magnification cap ----------------

        [TestCase(50, 25)]
        [TestCase(20, 10)]
        public void QrCap_MaxMagnificationAlwaysFitsInsideTheMargins(int widthMm, int heightMm)
        {
            var payload = LabelQrSizing.DefaultWorstCasePayloadBytes;
            var max = LabelQrSizing.MaxMagnificationFor(widthMm, heightMm, payload);
            var usableDots = (Math.Min(widthMm, heightMm) - 2 * LabelQrSizing.MarginMm) * LabelQrSizing.DotsPerMm;
            Console.WriteLine(widthMm + "x" + heightMm + "mm: modules=" + LabelQrSizing.ModulesForBytes(payload) + " usableDots=" + usableDots + " maxMag=" + max
                + " qr=" + LabelQrSizing.QrSizeMm(max, payload) + "mm");
            Assert.LessOrEqual(LabelQrSizing.QrSizeDots(max, payload), usableDots, "capped QR fits");
            if (max < LabelQrSizing.MaxMagnification)
            {
                Assert.Greater(LabelQrSizing.QrSizeDots(max + 1, payload), usableDots, "one step more would overflow");
            }

            Assert.AreEqual((decimal)max, LabelQrSizing.CapMagnification(10m, widthMm, heightMm, payload), "mag 10 is capped");
            Assert.AreEqual(1m, LabelQrSizing.CapMagnification(1m, widthMm, heightMm, payload), "small values are kept");
        }

        [Test]
        public void QrCap_LongerPayloadMeansMoreModulesAndASmallerCap()
        {
            Assert.Less(LabelQrSizing.ModulesForBytes(20), LabelQrSizing.ModulesForBytes(120));
            Assert.GreaterOrEqual(LabelQrSizing.MaxMagnificationFor(50, 25, 20), LabelQrSizing.MaxMagnificationFor(50, 25, 120));
        }

        // ---------------- item 2: deactivated user login ----------------

        [Test]
        public void InactiveLogin_OnlyAVerifiedPasswordRevealsTheDeactivatedAccount()
        {
            var inactive = new ApplicationUser { Id = "u-inactive", IsActive = false, PasswordHash = PasswordHasher.HashPassword("Correct#Pass123") };
            var active = new ApplicationUser { Id = "u-active", IsActive = true, PasswordHash = PasswordHasher.HashPassword("Correct#Pass123") };
            Assert.AreSame(inactive, InactiveAccountLoginRules.FindVerifiedInactive(new[] { active, inactive }, "Correct#Pass123"));
            Assert.IsNull(InactiveAccountLoginRules.FindVerifiedInactive(new[] { inactive }, "Wrong#Pass123"), "wrong password = generic failure");
            Assert.IsNull(InactiveAccountLoginRules.FindVerifiedInactive(new[] { active }, "Correct#Pass123"), "active users are not handled here");
            Assert.IsNull(InactiveAccountLoginRules.FindVerifiedInactive(new[] { inactive }, ""));
            Assert.AreEqual("LOGIN_DENIED_INACTIVE", InactiveAccountLoginRules.LoginDeniedInactiveEventType);
        }

        // ---------------- item 3: platform admin bootstrap ----------------

        [Test]
        public void Bootstrap_IsInertWithoutKeys_AndNormalisesTheEmail()
        {
            Assert.IsNull(PlatformAdminBootstrap.ReadConfig(new NameValueCollection()));
            Assert.IsFalse(PlatformAdminBootstrap.AnyKeysPresent(new NameValueCollection { { "SmtpHost", "x" } }));
            var settings = new NameValueCollection { { "Bootstrap:PlatformAdminEmail", "  Allan@NTLAfrica.com " }, { "Bootstrap:ClearMfaEnrollment", "TRUE" } };
            Assert.IsTrue(PlatformAdminBootstrap.AnyKeysPresent(settings));
            var config = PlatformAdminBootstrap.ReadConfig(settings);
            Assert.AreEqual("allan@ntlafrica.com", config.Email);
            Assert.IsTrue(config.ClearMfaEnrollment);
            Assert.IsFalse(config.HasPassword);
            Assert.IsNull(PlatformAdminBootstrap.ValidateConfig(config));
            Assert.IsFalse(PlatformAdminBootstrap.ReadConfig(new NameValueCollection { { "Bootstrap:PlatformAdminEmail", "a@b.co" } }).ClearMfaEnrollment, "default false");
        }

        [Test]
        public void Bootstrap_RefusesBadEmailAndWeakPassword()
        {
            Assert.IsNotNull(PlatformAdminBootstrap.ValidateConfig(new PlatformAdminBootstrapConfig { Email = "not-an-email" }));
            Assert.IsNotNull(PlatformAdminBootstrap.ValidateConfig(new PlatformAdminBootstrapConfig { Email = "a@b.co", Password = "short" }));
            Assert.IsNull(PlatformAdminBootstrap.ValidateConfig(new PlatformAdminBootstrapConfig { Email = "a@b.co", Password = "Str0ng!Passw0rd#2026" }));
        }

        [Test]
        public void Bootstrap_MarkerRunsOncePerKeySet_AndNeverContainsThePassword()
        {
            var withPassword = new PlatformAdminBootstrapConfig { Email = "allan@ntlafrica.com", Password = "Str0ng!Passw0rd#2026" };
            var noPassword = new PlatformAdminBootstrapConfig { Email = "allan@ntlafrica.com" };
            var key = PlatformAdminBootstrap.BuildMarkerKey(withPassword);
            Assert.AreEqual(key, PlatformAdminBootstrap.BuildMarkerKey(new PlatformAdminBootstrapConfig { Email = "allan@ntlafrica.com", Password = "Other!Passw0rd#2026" }), "same key set shape");
            Assert.AreNotEqual(key, PlatformAdminBootstrap.BuildMarkerKey(noPassword));
            Assert.AreNotEqual(key, PlatformAdminBootstrap.BuildMarkerKey(new PlatformAdminBootstrapConfig { Email = "other@ntlafrica.com", Password = "x" }));
            StringAssert.StartsWith(PlatformAdminBootstrap.MarkerKeyPrefix, key);

            var value = PlatformAdminBootstrap.BuildMarkerValue(withPassword, "user-1", DateTime.UtcNow);
            StringAssert.DoesNotContain("Str0ng!Passw0rd#2026", value);
            Assert.IsTrue(PlatformAdminBootstrap.MarkerMatches(value, withPassword), "same password = already applied");
            Assert.IsFalse(PlatformAdminBootstrap.MarkerMatches(value, new PlatformAdminBootstrapConfig { Email = withPassword.Email, Password = "Other!Passw0rd#2026" }), "new password = run again");
            Assert.IsTrue(PlatformAdminBootstrap.MarkerMatches(PlatformAdminBootstrap.BuildMarkerValue(noPassword, "user-1", DateTime.UtcNow), noPassword));
            Assert.IsFalse(PlatformAdminBootstrap.MarkerMatches(null, noPassword));
        }

        // ---------------- item 4: PO from requisition ----------------

        private static FakeUnitOfWork SeedRequisition(bool withLines = true, ApprovalStatus status = ApprovalStatus.Approved, int? targetAssetId = null)
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, Name = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 1, Name = "Laptop", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 2, Name = "Printer", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetSubType { Id = 10, AssetTypeId = 1, Name = "Dell Latitude 5440", Brand = "Dell", Model = "Latitude 5440", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 20, AssetTypeId = 2, Name = "HP LaserJet", Brand = "HP", Model = "LaserJet", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new Department { Id = 1, Name = "Administration", Code = "ADM", DepartmentKind = DepartmentKind.Administrative, CreatedAt = DateTime.UtcNow, IsActive = true, IsRequisitionTarget = true });
            uow.Seed(new Department { Id = 4, Name = "Room 4", Code = "R4", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new PurchaseRequest
            {
                Id = 100, RequestNumber = "PR-100", DepartmentId = 1, ApprovalStatus = status, ItemDescription = "Laptops and printers",
                Quantity = withLines ? 5 : 5, Currency = "KES", RequestedById = "requester", TargetAssetId = targetAssetId,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            if (withLines)
            {
                uow.Seed(new PurchaseRequestLine { Id = 1, PurchaseRequestId = 100, LineNumber = 1, Description = "Laptop", Quantity = 3, IsActive = true, CreatedAt = DateTime.UtcNow });
                uow.Seed(new PurchaseRequestLine { Id = 2, PurchaseRequestId = 100, LineNumber = 2, Description = "Printer", Quantity = 2, IsActive = true, CreatedAt = DateTime.UtcNow });
            }

            return uow;
        }

        private static PurchaseRecordVm Order(int line1, int line2, int? type1 = 1, int? sub1 = 10, int? type2 = null)
        {
            return new PurchaseRecordVm
            {
                PurchaseRequestId = 100, PurchaseOrderNumber = "PO-1", SupplierId = 1, PurchaseDate = DateTime.UtcNow, Currency = "KES",
                Lines = new List<PurchaseOrderLineInputVm>
                {
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 1, Quantity = line1, UnitCost = 1000m, AssetTypeId = type1, AssetSubTypeId = sub1 },
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 2, Quantity = line2, UnitCost = 500m, AssetTypeId = type2 }
                }
            };
        }

        [Test]
        public void PurchaseOrder_RequiresAnApprovedRequisition()
        {
            var uow = SeedRequisition(status: ApprovalStatus.Pending);
            var service = TestServiceFactory.CreatePurchaseService(uow);
            var ex = Assert.Throws<BusinessException>(() => service.Create(new PurchaseRecordVm { SupplierId = 1, Quantity = 1, UnitCost = 1 }));
            Assert.AreEqual(PurchaseService.RequisitionRequiredMessage, ex.Message);
            Assert.Throws<BusinessException>(() => service.Create(Order(1, 0)), "pending requisition");
            Assert.IsNull(service.BuildFromRequisition(100));
        }

        [Test]
        public void PurchaseOrder_PrefillsLinesFromTheRequisition_AndStoresTheLink()
        {
            var uow = SeedRequisition();
            var service = TestServiceFactory.CreatePurchaseService(uow);
            var draft = service.BuildFromRequisition(100);
            Assert.AreEqual(2, draft.Lines.Count);
            Assert.AreEqual(new[] { 3, 2 }, draft.Lines.Select(x => x.Outstanding).ToArray());
            Assert.AreEqual(new[] { "Laptop", "Printer" }, draft.Lines.Select(x => x.Description).ToArray());

            var id = service.Create(Order(2, 2));
            var po = uow.Repository<PurchaseRecord>().GetById(id);
            Assert.AreEqual(100, po.PurchaseRequestId);
            Assert.AreEqual(4, po.Quantity);
            Assert.AreEqual(3000m, po.TotalCost);
            var lines = uow.Repository<PurchaseOrderLine>().GetAll().Where(x => x.PurchaseRecordId == id).OrderBy(x => x.LineNumber).ToList();
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(1, lines[0].PurchaseRequestLineId);
            Assert.AreEqual("Laptop", lines[0].Description, "description from the requisition, not the post");
            Assert.AreEqual(1, lines[0].AssetTypeId);
            Assert.AreEqual(10, lines[0].AssetSubTypeId);
            Assert.AreEqual(1, lines[0].AssetCategoryId);
            Assert.AreEqual(PurchaseOrderLedger.SourceSelectedOnOrder, lines[0].ClassificationSource);
            Assert.IsNull(lines[1].AssetTypeId, "no type chosen and no catalog = none");

            var status = service.GetOrderStatus(id);
            Assert.IsFalse(status.IsLegacy);
            Assert.AreEqual(PurchaseOrderQuantityRules.StatusOpen, status.Status);
            var summary = service.GetRequisitionOrderSummary(100);
            Assert.AreEqual(4, summary.OrderedQuantity);
            Assert.AreEqual(1, summary.OutstandingToOrder);
            Assert.AreEqual(PurchaseOrderQuantityRules.FulfilmentPartiallyOrdered, summary.FulfilmentStatus);
        }

        [Test]
        public void PurchaseOrder_BlocksOverOrdering_CumulativeAcrossPurchaseOrders()
        {
            var uow = SeedRequisition();
            var service = TestServiceFactory.CreatePurchaseService(uow);
            service.Create(Order(2, 2));
            var ex = Assert.Throws<BusinessException>(() => service.Create(Order(2, 0)));
            StringAssert.Contains("Over-ordering", ex.Message);
            Assert.Throws<BusinessException>(() => service.Create(Order(0, 1)), "line 2 already fully ordered");
            Assert.Throws<BusinessException>(() => service.Create(Order(0, 0)), "nothing to order");
            service.Create(Order(1, 0));
            Assert.AreEqual(0, service.BuildFromRequisition(100).Lines.Sum(x => x.Outstanding));
            Assert.IsEmpty(service.GetRequisitionsReadyForPurchaseOrder(new[] { 100 }), "fully ordered requisitions leave the picker");
            Assert.AreEqual(PurchaseOrderQuantityRules.FulfilmentFullyOrdered, service.GetRequisitionOrderSummary(100).FulfilmentStatus);
        }

        [Test]
        public void PurchaseOrder_RefusesLinesFromAnotherRequisition_AndBadSubTypes()
        {
            var uow = SeedRequisition();
            uow.Seed(new PurchaseRequestLine { Id = 9, PurchaseRequestId = 999, LineNumber = 1, Description = "Other", Quantity = 9, IsActive = true, CreatedAt = DateTime.UtcNow });
            var service = TestServiceFactory.CreatePurchaseService(uow);
            var foreign = Order(1, 0);
            foreign.Lines[0].PurchaseRequestLineId = 9;
            Assert.Throws<BusinessException>(() => service.Create(foreign));
            Assert.Throws<BusinessException>(() => service.Create(Order(1, 0, type1: 1, sub1: 20)), "sub-type of another type");
        }

        [Test]
        public void PurchaseOrder_SingleItemRequisition_UsesTheRequisitionQuantity()
        {
            var uow = SeedRequisition(withLines: false);
            var service = TestServiceFactory.CreatePurchaseService(uow);
            var draft = service.BuildFromRequisition(100);
            Assert.AreEqual(1, draft.Lines.Count);
            Assert.IsNull(draft.Lines[0].PurchaseRequestLineId);
            Assert.AreEqual(5, draft.Lines[0].Outstanding);
            Assert.Throws<BusinessException>(() => service.Create(new PurchaseRecordVm { PurchaseRequestId = 100, SupplierId = 1, Quantity = 6, UnitCost = 10m, PurchaseDate = DateTime.UtcNow }));
            var id = service.Create(new PurchaseRecordVm { PurchaseRequestId = 100, SupplierId = 1, Quantity = 5, UnitCost = 10m, PurchaseDate = DateTime.UtcNow });
            Assert.AreEqual(1, uow.Repository<PurchaseOrderLine>().GetAll().Count(x => x.PurchaseRecordId == id && x.PurchaseRequestLineId == null));
        }

        [Test]
        public void PurchaseOrder_RequisitionAssetFixesTheClassification()
        {
            var uow = SeedRequisition(targetAssetId: 40);
            uow.Seed(new Asset { Id = 40, AssetTag = "T-40", AssetName = "Template", CategoryId = 1, AssetTypeId = 2, AssetSubTypeId = 20, IsActive = true, CreatedAt = DateTime.UtcNow, PurchaseDate = DateTime.UtcNow, DepreciationStartDate = DateTime.UtcNow });
            var service = TestServiceFactory.CreatePurchaseService(uow);
            Assert.IsTrue(service.BuildFromRequisition(100).Lines.All(x => x.ClassificationLocked));
            var id = service.Create(Order(1, 1, type1: 1, sub1: 10));
            var lines = uow.Repository<PurchaseOrderLine>().GetAll().Where(x => x.PurchaseRecordId == id).ToList();
            Assert.IsTrue(lines.All(x => x.AssetTypeId == 2 && x.AssetSubTypeId == 20 && x.ClassificationSource == PurchaseOrderLedger.SourceRequisitionAsset), "posted type ignored");
        }

        // ---------------- item 5: receiving ----------------

        private static ReceiveOrderLineVm Line(int lineId, int qty, string serials = null, int? overrideType = null)
        {
            return new ReceiveOrderLineVm
            {
                PurchaseOrderLineId = lineId, QuantityReceived = qty, Condition = "New", SerialNumbersText = serials,
                TagMode = ReceiveOrderLineVm.TagModeGenerate, DestinationKind = ReceiveOrderLineVm.DestinationStore, OverrideAssetTypeId = overrideType
            };
        }

        private static int CreateOrder(FakeUnitOfWork uow)
        {
            return TestServiceFactory.CreatePurchaseService(uow).Create(Order(2, 1));
        }

        [Test]
        public void Receiving_ShowsOrderedReceivedOutstanding_AndAutoFillsClassification()
        {
            var uow = SeedRequisition();
            var poId = CreateOrder(uow);
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var screen = receiving.GetReceiveOrder(poId);
            Assert.AreEqual(2, screen.Lines.Count);
            Assert.AreEqual(3, screen.Outstanding);
            Assert.IsFalse(screen.Lines[0].NeedsClassification);
            Assert.AreEqual("IT", screen.Lines[0].AssetCategoryName);
            Assert.AreEqual("Laptop", screen.Lines[0].AssetTypeName);
            Assert.AreEqual("Dell Latitude 5440", screen.Lines[0].AssetSubTypeName);
            Assert.IsTrue(screen.Lines[1].NeedsClassification, "printer line has no source");
            Assert.AreEqual(1, screen.RequisitionDepartmentId);
        }

        [Test]
        public void Receiving_PartialThenFull_BlocksOverReceipt_AndUpdatesStatus()
        {
            var uow = SeedRequisition();
            var poId = CreateOrder(uow);
            var lineIds = uow.Repository<PurchaseOrderLine>().GetAll().Where(x => x.PurchaseRecordId == poId).OrderBy(x => x.LineNumber).Select(x => x.Id).ToList();
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            // Partial: 1 laptop with a serial; posted override type is IGNORED because the line has a type.
            var first = Line(lineIds[0], 1, "SN-A1", overrideType: 2);
            var result = receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { first } }, "receiver-1");
            Assert.AreEqual(1, result.CreatedAssets.Count);
            var asset = uow.Repository<Asset>().GetById(result.CreatedAssets[0].AssetId);
            Assert.AreEqual(1, asset.AssetTypeId, "source type wins over the post");
            Assert.AreEqual(1, asset.CategoryId);
            Assert.AreEqual(10, asset.AssetSubTypeId);
            Assert.AreEqual("SN-A1", asset.SerialNumber);
            Assert.AreEqual(AssetStatus.InStore, asset.CurrentStatus);
            Assert.IsNull(asset.DepartmentId, "store = company custody");
            Assert.IsFalse(string.IsNullOrWhiteSpace(asset.AssetTag), "generated tag");
            var status = TestServiceFactory.CreatePurchaseService(uow).GetOrderStatus(poId);
            Assert.AreEqual(PurchaseOrderQuantityRules.StatusPartiallyReceived, status.Status);
            Assert.AreEqual(1, status.Lines[0].Received);
            Assert.AreEqual(1, uow.Repository<PurchaseOrderLineReceipt>().GetAll().Count(x => x.PurchaseOrderLineId == lineIds[0]));

            // Over-receipt on line 1 (1 outstanding) is refused; nothing is created.
            var assetsBefore = uow.Repository<Asset>().GetAll().Count();
            var ex = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { Line(lineIds[0], 2) } }, "receiver-1"));
            StringAssert.Contains("Over-receipt", ex.Message);
            Assert.AreEqual(assetsBefore, uow.Repository<Asset>().GetAll().Count());

            // The printer line has no classification: refused without an override, accepted with one; to a room.
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { Line(lineIds[1], 1) } }, "receiver-1"));
            var printer = Line(lineIds[1], 1, overrideType: 2);
            printer.DestinationKind = ReceiveOrderLineVm.DestinationDepartment;
            printer.DestinationDepartmentId = 4;
            printer.Condition = "Good";
            var rest = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { Line(lineIds[0], 1), printer }
            }, "receiver-1");
            Assert.AreEqual(2, rest.CreatedAssets.Count);
            var printerAsset = uow.Repository<Asset>().GetById(rest.CreatedAssets[1].AssetId);
            Assert.AreEqual(2, printerAsset.AssetTypeId);
            Assert.AreEqual(4, printerAsset.DepartmentId);
            Assert.AreEqual("Good", printerAsset.ConditionOnReceipt);

            Assert.AreEqual(PurchaseOrderQuantityRules.StatusReceived, TestServiceFactory.CreatePurchaseService(uow).GetOrderStatus(poId).Status);
            Assert.IsNotNull(uow.Repository<PurchaseRecord>().GetById(poId).ReceivedDate, "PO stamped received");
            Assert.AreEqual(PurchaseOrderQuantityRules.FulfilmentPartiallyReceived, TestServiceFactory.CreatePurchaseService(uow).GetRequisitionOrderSummary(100).FulfilmentStatus, "3 of 5 received");
        }

        [Test]
        public void Receiving_ValidatesSerialsTagsConditionAndDestination()
        {
            var uow = SeedRequisition();
            var poId = CreateOrder(uow);
            var laptopLine = uow.Repository<PurchaseOrderLine>().GetAll().First(x => x.PurchaseRecordId == poId && x.LineNumber == 1).Id;
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            Func<ReceiveOrderLineVm, ReceiveOrderVm> wrap = l => new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { l } };

            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(wrap(Line(laptopLine, 2, "ONLY-ONE")), "r"), "serial count");
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(wrap(Line(laptopLine, 2, "DUP\nDUP")), "r"), "duplicate serial");
            var badCondition = Line(laptopLine, 1);
            badCondition.Condition = "Shiny";
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(wrap(badCondition), "r"));
            var noDept = Line(laptopLine, 1);
            noDept.DestinationKind = ReceiveOrderLineVm.DestinationDepartment;
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(wrap(noDept), "r"));
            var tagMismatch = Line(laptopLine, 2);
            tagMismatch.TagMode = ReceiveOrderLineVm.TagModeEnter;
            tagMismatch.AssetTagsText = "TAG-1";
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(wrap(tagMismatch), "r"));
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { Line(laptopLine, 0) } }, "r"), "nothing entered");
            Assert.AreEqual(0, uow.Repository<AssetReceiving>().GetAll().Count(), "no partial writes");

            var tagged = Line(laptopLine, 2, "S-1\r\nS-2");
            tagged.TagMode = ReceiveOrderLineVm.TagModeEnter;
            tagged.AssetTagsText = "LAP-0001\nLAP-0002";
            var result = receiving.ReceiveOrder(wrap(tagged), "r");
            CollectionAssert.AreEquivalent(new[] { "LAP-0001", "LAP-0002" }, result.CreatedAssets.Select(x => x.AssetTag).ToArray());
        }

        [Test]
        public void Receiving_LegacyPurchaseOrder_UsesOneVirtualLine_AndTheOldFlowRefusesLinedOrders()
        {
            var uow = SeedRequisition(withLines: false);
            uow.Seed(new PurchaseRecord { Id = 77, PurchaseRequestId = null, SupplierId = 1, Quantity = 2, UnitCost = 10m, TotalCost = 20m, PurchaseDate = DateTime.UtcNow, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var screen = receiving.GetReceiveOrder(77);
            Assert.IsTrue(screen.IsLegacy);
            Assert.AreEqual(0, screen.Lines.Single().PurchaseOrderLineId);
            Assert.IsTrue(screen.Lines[0].NeedsClassification);
            receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = 77, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { Line(0, 2, overrideType: 1) } }, "r");
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm { PurchaseRecordId = 77, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { Line(0, 1, overrideType: 1) } }, "r"));

            var linedPo = TestServiceFactory.CreatePurchaseService(uow).Create(new PurchaseRecordVm { PurchaseRequestId = 100, SupplierId = 1, Quantity = 1, UnitCost = 1m, PurchaseDate = DateTime.UtcNow });
            Assert.Throws<BusinessException>(() => receiving.Receive(new AssetReceiveVm { PurchaseRecordId = linedPo, ReceivedDate = DateTime.UtcNow, QuantityReceived = 1, ConditionOnReceipt = "New", AssetTypeId = 1, ReceivePlacementChoice = ReceivingService.PlacementCompanyCustody }, "r"));
        }

        [Test]
        public void QuantityRules_ParseListAndStatuses()
        {
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, PurchaseOrderQuantityRules.ParseList(" A \r\n\r\nB,C "));
            Assert.AreEqual(PurchaseOrderQuantityRules.StatusOpen, PurchaseOrderQuantityRules.ReceiptStatus(5, 0));
            Assert.AreEqual(PurchaseOrderQuantityRules.StatusPartiallyReceived, PurchaseOrderQuantityRules.ReceiptStatus(5, 2));
            Assert.AreEqual(PurchaseOrderQuantityRules.StatusReceived, PurchaseOrderQuantityRules.ReceiptStatus(5, 5));
            Assert.AreEqual(PurchaseOrderQuantityRules.FulfilmentNotOrdered, PurchaseOrderQuantityRules.FulfilmentStatus(5, 0, 0));
            Assert.AreEqual(PurchaseOrderQuantityRules.FulfilmentReceived, PurchaseOrderQuantityRules.FulfilmentStatus(5, 5, 5));
        }
    }
}
