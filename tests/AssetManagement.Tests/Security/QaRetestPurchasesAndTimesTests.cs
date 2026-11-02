using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Tests.Requisitions;
using AssetManagement.Web.Controllers;
using AssetManagement.Web.Filters;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// QA retest of 4cb3a03 (2026-11-02): (1) the Purchases list "New Purchase Record" button opens Purchases/Create,
    /// the same action as "Record purchase order" on an approved requisition, so it is shown exactly when that action
    /// accepts the user (Purchases.View on the controller + Purchases.Edit on the action), not on Purchases.Create;
    /// (2) requisition Submitted / Approved times are stored in UTC and shown in Kenya time (EAT) via KenyaTimeHelper,
    /// like Asset requests, Incidents, Claims and the approval history.
    /// </summary>
    [TestFixture]
    public class QaRetestPurchasesAndTimesTests
    {
        private static Func<string, bool> Grants(params string[] codes)
        {
            var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            return code => set.Contains(code);
        }

        [Test]
        public void NewPurchaseRecordButton_TargetsPurchasesCreate_GatedLikeTheAction()
        {
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Purchases", "Index.cshtml");
            StringAssert.Contains("RequisitionAccessRules.HasPurchaseOrderPermission(code => Html.HasPermission(code))", view);
            StringAssert.DoesNotContain("Html.HasPermission(\"Purchases.Create\")", view);
            StringAssert.Contains("PrimaryActionUrl = canCreatePurchase ? Url.Action(\"Create\") : null", view);

            var details = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.Contains("Url.Action(\"Create\", \"Purchases\", new { purchaseRequestId = Model.Id", details);

            // The action the button opens: controller filter Purchases.View, both Create overloads Purchases.Edit.
            var controller = typeof(PurchasesController);
            var classAttr = controller.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
            Assert.AreEqual(RequisitionAccessRules.ViewPermission, classAttr.ConstructorArguments[0].Value);
            var creates = controller.GetMethods().Where(m => m.Name == "Create" && m.DeclaringType == controller).ToList();
            Assert.AreEqual(2, creates.Count);
            foreach (var method in creates)
            {
                var attr = method.GetCustomAttributesData().Single(a => a.Constructor.DeclaringType == typeof(PermissionAuthorizeAttribute));
                Assert.AreEqual(RequisitionAccessRules.EditPermission, attr.ConstructorArguments[0].Value);
            }
        }

        [Test]
        public void NewPurchaseRecordButton_ShownForFinanceOfficer_ViewPlusEdit_WithoutCreate()
        {
            Assert.IsTrue(RequisitionAccessRules.HasPurchaseOrderPermission(Grants("Purchases.View", "Purchases.Edit", "Purchases.Approve")),
                "Finance Officer (View + Edit + Approve, no Purchases.Create) can record a purchase order");
            Assert.IsFalse(RequisitionAccessRules.HasPurchaseOrderPermission(Grants("Purchases.View", "Purchases.Create")),
                "Purchases.Create (raise a requisition) alone does not open Purchases/Create");
        }

        [Test]
        public void RequisitionTimes_StoredUtc_ShownInKenyaTime()
        {
            // QA: submitted 15:43 EAT was shown as 12:43.
            var utc = new DateTime(2026, 11, 2, 12, 43, 0, DateTimeKind.Utc);
            Assert.AreEqual("2026-11-02 15:43", KenyaTimeHelper.FormatUtc(utc, KenyaTimeHelper.ShortTimestampFormat));
            Assert.AreEqual("2026-11-02 15:43",
                KenyaTimeHelper.FormatUtc(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), KenyaTimeHelper.ShortTimestampFormat),
                "EF loads datetime columns as Unspecified; they are treated as UTC");

            var service = QaSixRequisitionFormTests.Read("src", "AssetManagement.Application", "Services", "Purchases", "PurchaseRequestService.cs");
            StringAssert.Contains("CreatedAt = DateTime.UtcNow", service);
            StringAssert.Contains("entity.ApprovedAt = DateTime.UtcNow;", service);

            var details = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Details.cshtml");
            StringAssert.Contains("KenyaTimeHelper.FormatUtc(Model.CreatedAt, KenyaTimeHelper.ShortTimestampFormat) @KenyaTimeHelper.DisplayAbbreviation", details);
            StringAssert.Contains("KenyaTimeHelper.FormatUtc(Model.ApprovedAt.Value, KenyaTimeHelper.ShortTimestampFormat) @KenyaTimeHelper.DisplayAbbreviation", details);
            var index = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Index.cshtml");
            StringAssert.Contains("KenyaTimeHelper.FormatUtc(r.CreatedAt, KenyaTimeHelper.ShortTimestampFormat)", index);
            var batch = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Batch.cshtml");
            StringAssert.Contains("KenyaTimeHelper.FormatUtc(Model.CreatedAt, KenyaTimeHelper.ShortTimestampFormat)", batch);

            foreach (var v in new[] { details, index, batch })
            {
                StringAssert.DoesNotContain("CreatedAt.ToString(\"yyyy-MM-dd HH:mm\")", v);
                StringAssert.DoesNotContain("ApprovedAt.Value.ToString(", v);
            }
        }
    }
}
