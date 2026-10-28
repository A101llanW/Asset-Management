using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>
    /// QA 2026-10-28 findings 1 and 2 (Allan 09:33 EAT): a type with no active sub-types is not offered and gets the
    /// clear message; a tagged line shows the asset's Category / Type / Sub-type read-only.
    /// </summary>
    [TestFixture]
    public class QaSixRequisitionFormTests
    {
        private const int ChairTypeId = 584;
        private const int LaptopTypeId = 10;

        private static bool? Lookup(int typeId)
        {
            if (typeId == ChairTypeId)
            {
                return false;
            }

            return typeId == LaptopTypeId ? (bool?)true : null;
        }

        private static PurchaseRequestCreateVm Form(params PurchaseRequestLineCreateVm[] lines)
        {
            return new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", Lines = lines.ToList() };
        }

        private static PurchaseRequestLineCreateVm Line(int? typeId, int? subTypeId = null, int? targetAssetId = null)
        {
            return new PurchaseRequestLineCreateVm
            {
                ItemName = "Chair",
                Description = "Office chair",
                Quantity = 1,
                AssetTypeId = typeId,
                AssetSubTypeId = subTypeId,
                TargetAssetId = targetAssetId
            };
        }

        [Test]
        public void RequisitionableOnly_DropsTypesWithoutSubTypes_KeepsTheRest()
        {
            var picker = new CatalogPickerVm
            {
                Categories = new List<CatalogPickerItemVm> { new CatalogPickerItemVm { Id = 1, Name = "Furniture" } },
                Types = new List<CatalogPickerItemVm>
                {
                    new CatalogPickerItemVm { Id = ChairTypeId, Name = "Office Chair", ParentId = 1 },
                    new CatalogPickerItemVm { Id = LaptopTypeId, Name = "Laptop", ParentId = 1 }
                },
                SubTypes = new List<CatalogPickerItemVm> { new CatalogPickerItemVm { Id = 100, Name = "14 inch", ParentId = LaptopTypeId } }
            };

            var filtered = picker.RequisitionableOnly();

            CollectionAssert.AreEqual(new[] { LaptopTypeId }, filtered.Types.Select(x => x.Id).ToArray());
            Assert.AreEqual(1, filtered.Categories.Count);
            Assert.AreEqual(1, filtered.SubTypes.Count);
            Assert.AreEqual(2, picker.Types.Count, "the source picker is not changed");
            CollectionAssert.AreEquivalent(new[] { LaptopTypeId }, picker.TypeIdsWithSubTypes().ToArray());
        }

        [Test]
        public void UntaggedLine_TypeWithoutSubTypes_IsFlagged()
        {
            var model = Form(Line(LaptopTypeId, 100), Line(ChairTypeId));

            CollectionAssert.AreEqual(new[] { 1 }, model.FindUntaggedLinesWithTypeLackingSubTypes(Lookup).ToArray());
        }

        [Test]
        public void TaggedLine_UnknownType_TypeWithSubTypes_AndNoType_AreNotFlagged()
        {
            var model = Form(Line(ChairTypeId, null, 25310), Line(999), Line(LaptopTypeId), Line(null));

            CollectionAssert.IsEmpty(model.FindUntaggedLinesWithTypeLackingSubTypes(Lookup));
            CollectionAssert.IsEmpty(model.FindUntaggedLinesWithTypeLackingSubTypes(null));
        }

        [Test]
        public void FlaggedLine_StillHasTheGenericError_UnderTheSameKey_ForTheControllerToReplace()
        {
            var model = Form(Line(ChairTypeId));
            var results = model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model, null, null)).ToList();

            var generic = results.Single(x => x.ErrorMessage == "Line 1: choose the asset type and sub-type.");
            CollectionAssert.AreEqual(new[] { "Lines[0].AssetSubTypeId" }, generic.MemberNames.ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, model.FindUntaggedLinesWithTypeLackingSubTypes(Lookup).ToArray());
        }

        [Test]
        public void NoActiveSubTypesMessage_MatchesTheServiceWording()
        {
            var message = PurchaseRequestCreateVm.NoActiveSubTypesMessage(2);
            Assert.AreEqual("Line 2: this asset type has no active sub-types yet. Add one under Asset Categories first; every requisitioned asset needs a sub-type.", message);
            var service = Read("src", "AssetManagement.Application", "Services", "Purchases", "PurchaseRequestService.cs");
            StringAssert.Contains(message.Substring("Line 2".Length), service);
        }

        [Test]
        public void Controller_SwapsTheMessage_BeforeTheModelStateCheck_AndOffersOnlyRequisitionableTypes()
        {
            var controller = Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            var create = controller.IndexOf("public ActionResult Create(PurchaseRequestCreateVm model", StringComparison.Ordinal);
            var swap = controller.IndexOf("ApplyNoActiveSubTypeMessages(model);", create, StringComparison.Ordinal);
            var check = controller.IndexOf("if (!ModelState.IsValid)", create, StringComparison.Ordinal);
            Assert.IsTrue(create > 0 && swap > create && swap < check, "the clear message is set before the post is stopped");

            var catalog = Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.Catalog.cs");
            StringAssert.Contains("var picker = allTypes.RequisitionableOnly();", catalog);
            StringAssert.Contains("PurchaseRequestCreateVm.NoActiveSubTypesMessage(index + 1)", catalog);
        }

        [Test]
        public void ClassificationScript_FiltersTypesWithoutSubTypes_AndSwapsPickersForReadOnlyFields()
        {
            var js = Read("src", "AssetManagement.Web", "Scripts", "app", "purchase-request-classification.js");
            StringAssert.Contains("catalog.types = catalog.types.filter(function (t) {", js);
            StringAssert.Contains("setHidden(block.querySelector(\".am-line-pickers\"), fromAsset);", js);
            StringAssert.Contains("setHidden(block.querySelector(\".am-line-asset-classification\"), !fromAsset);", js);
            AssertIe11Safe(js);
        }

        [Test]
        public void CreateScript_FillsTheTaggedAssetClassification()
        {
            var js = Read("src", "AssetManagement.Web", "Scripts", "app", "purchase-request-create.js");
            StringAssert.Contains("selectButton.setAttribute(\"data-asset-type\", item.assetTypeName || \"\");", js);
            StringAssert.Contains("setRowAssetClassification(row, tagged ? classification : null);", js);
            AssertIe11Safe(js);

            var controller = Read("src", "AssetManagement.Web", "Controllers", "PurchaseRequestsController.cs");
            StringAssert.Contains("assetTypeName = string.IsNullOrWhiteSpace(x.AssetTypeName) ? \"\" : x.AssetTypeName,", controller);
        }

        [Test]
        public void View_RendersReadOnlyClassification_ForServerRenderedAndTemplateRows()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Create.cshtml");
            Assert.AreEqual(2, Regex.Matches(view, "am-line-asset-classification").Count, "row markup and the template");
            Assert.AreEqual(2, Regex.Matches(view, "am-line-pickers").Count);
            StringAssert.Contains("value=\"@lineTargetClass[1]\"", view);
            Assert.IsFalse(Regex.IsMatch(view, "am-line-asset-(category|type|subtype)\"[^>]*\\bname="), "read-only fields are never posted");
        }

        [Test]
        public void FormatTargetAssetClassification_IsCategoryTypeSubType()
        {
            var asset = new AssetListVm { CategoryName = "Furniture", AssetTypeName = "Desk", AssetSubTypeName = null };
            CollectionAssert.AreEqual(new[] { "Furniture", "Desk", "" }, PurchaseRequestsController.FormatTargetAssetClassification(asset));
            CollectionAssert.AreEqual(new[] { "", "", "" }, PurchaseRequestsController.FormatTargetAssetClassification(null));
        }

        internal static void AssertIe11Safe(string js)
        {
            Assert.IsFalse(js.Contains("=>"), "no arrow functions (IE11)");
            Assert.IsFalse(Regex.IsMatch(js, @"\b(let|const|class)\s"), "no let/const/class (IE11)");
            Assert.IsFalse(js.Contains("`"), "no template literals (IE11)");
        }

        internal static string Read(params string[] parts)
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Web")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Repository root not found.");
            }

            return File.ReadAllText(Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray()));
        }
    }
}
