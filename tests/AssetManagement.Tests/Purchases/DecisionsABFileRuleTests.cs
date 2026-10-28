using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>
    /// Allan 2026-10-28 08:39 EAT, decisions A and B: the view / script side (the service rules are covered in
    /// RequisitionLineAssetNameTests).
    /// </summary>
    [TestFixture]
    public class DecisionsABFileRuleTests
    {
        [Test]
        public void A_RequisitionForm_ShowsTheDescriptionErrorUnderTheRow()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "PurchaseRequests", "Create.cshtml");
            StringAssert.Contains("@Html.ValidationMessage(\"Lines[\" + i + \"].Description\"", view);
        }

        [Test]
        public void B_ReceiveView_PostsTheOpenedFlag_OnlyWhileEditing_AndHasTheClearedMessage()
        {
            var view = Read("src", "AssetManagement.Web", "Views", "Purchases", "Receive.cshtml");
            StringAssert.Contains("<input type=\"hidden\" class=\"am-edit-opened\" name=\"Lines[@i].ClassificationEditOpened\" value=\"true\" @(editing ? \"\" : \"disabled\") />", view);
            var editor = view.IndexOf("am-recv-class-editor@", StringComparison.Ordinal);
            var flag = view.IndexOf("am-edit-opened", StringComparison.Ordinal);
            var cancel = view.IndexOf("am-recv-class-cancel", StringComparison.Ordinal);
            Assert.IsTrue(editor > 0 && flag > editor && flag < cancel, "the flag sits inside the Edit panel");
            StringAssert.Contains("am-edit-class-missing d-none", view);
        }

        [Test]
        public void B_ReceiveScript_TogglesTheFlag_AndBlocksACleared_OpenPanel()
        {
            var js = Read("src", "AssetManagement.Web", "Scripts", "app", "receive-order.js");
            StringAssert.Contains("opened.disabled = !editing;", js);
            StringAssert.Contains("function flagClearedEditors(form)", js);
            StringAssert.Contains("var missing = flagMissingSubTypes(form) + flagClearedEditors(form);", js);
            StringAssert.Contains("(!typeSelect.value || !subSelect.value)", js);
            Assert.IsFalse(js.Contains("=>"), "no arrow functions (IE11)");
            Assert.IsFalse(Regex.IsMatch(js, @"\b(let|const|class)\s"), "no let/const/class (IE11)");
            Assert.IsFalse(js.Contains("`"), "no template literals (IE11)");
        }

        [Test]
        public void B_Controller_ReopensAPanelTheUserOpened()
        {
            var controller = Read("src", "AssetManagement.Web", "Controllers", "PurchasesController.cs");
            StringAssert.Contains("&& (input.ClassificationEditOpened", controller);
        }

        [Test]
        public void B_LaterCompleteClassificationBackstop_IsKept()
        {
            var service = Read("src", "AssetManagement.Application", "Services", "Purchases", "ReceivingService.cs");
            StringAssert.Contains("public static void EnsureCompleteClassification(PurchaseLineClassification classification, string label)", service);
            Assert.Less(service.IndexOf("if (input.ClassificationEditOpened", StringComparison.Ordinal),
                service.IndexOf("if (input.OverrideAssetTypeId.HasValue && input.OverrideAssetTypeId.Value > 0)", StringComparison.Ordinal),
                "the cleared-panel check runs before the PO-line fallback");
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Web")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("Repository root not found.");
            return null;
        }

        private static string Read(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));
        }
    }
}
