using AssetManagement.Tests.Requisitions;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>
    /// QA 2026-10-28 finding 3 (Allan 09:33 EAT "Fix"): the PO lines table scrolled sideways at 1024 px and squashed the
    /// qty / cost inputs (about 60 px, values unreadable). Checked in headless Chrome at 1007 / 1024 / 1280 px with a
    /// static mock of the rendered table; these rules keep the markup that makes it fit.
    /// </summary>
    [TestFixture]
    public class QaSixPoLinesLayoutTests
    {
        private static string CreateView()
        {
            return QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Purchases", "Create.cshtml");
        }

        [Test]
        public void CreateTable_FoldsRequisitionAndAlreadyOrdered_BelowXl()
        {
            var view = CreateView();
            StringAssert.Contains("<th class=\"text-end d-none d-xl-table-cell\">Requisition</th>", view);
            StringAssert.Contains("<th class=\"text-end d-none d-xl-table-cell\">Already ordered</th>", view);
            StringAssert.Contains("<td class=\"text-end d-none d-xl-table-cell\">@line.RequisitionQuantity</td>", view);
            StringAssert.Contains("<td class=\"text-end d-none d-xl-table-cell\">@line.AlreadyOrdered</td>", view);
            StringAssert.Contains("<div class=\"small text-muted d-xl-none am-po-qty-context\">of @line.RequisitionQuantity, @line.AlreadyOrdered ordered</div>", view);
        }

        [Test]
        public void CreateTable_FooterColspans_MatchTheVisibleColumns()
        {
            var view = CreateView();
            StringAssert.Contains("<th colspan=\"3\" class=\"text-end d-xl-none\">Total</th>", view);
            StringAssert.Contains("<th colspan=\"5\" class=\"text-end d-none d-xl-table-cell\">Total</th>", view);
            Assert.IsFalse(view.Contains("<th colspan=\"5\" class=\"text-end\">Total</th>"));
        }

        [Test]
        public void CreateTable_HasNoFixedPixelColumns_AndKeepsTheE2eIds()
        {
            var view = CreateView();
            Assert.IsFalse(view.Contains("min-width:260px"));
            Assert.IsFalse(view.Contains("style=\"width:110px\""));
            Assert.IsFalse(view.Contains("style=\"width:130px\""));
            StringAssert.Contains("<th class=\"am-po-qty-col\">Order qty</th>", view);
            StringAssert.Contains("<th class=\"am-po-cost-col\">Unit cost</th>", view);
            StringAssert.Contains("<th class=\"am-po-class-col\">Classification</th>", view);
            StringAssert.Contains("id=\"@(i == 0 ? \"Quantity\" : \"Lines_\" + i + \"_Quantity\")\"", view);
            StringAssert.Contains("id=\"@(i == 0 ? \"UnitCost\" : \"Lines_\" + i + \"_UnitCost\")\"", view);
        }

        [Test]
        public void Css_GivesTheInputsAFloor_AndLetsLongTextWrap()
        {
            var css = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Content", "css", "site.css");
            StringAssert.Contains("#po-lines-table .am-po-qty { min-width: 4.5rem; }", css);
            StringAssert.Contains("#po-lines-table .am-po-cost { min-width: 7rem; }", css);
            StringAssert.Contains("#po-lines-table .am-po-class-col { min-width: 180px; }", css);
            StringAssert.Contains("#po-lines-card .am-po-class-cell { overflow-wrap: anywhere; }", css);
        }

        [Test]
        public void DetailsTable_WrapsClassification_AndKeepsMoneyOnOneLine()
        {
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Purchases", "Details.cshtml");
            StringAssert.Contains("<th class=\"am-po-class-col\">Classification</th>", view);
            StringAssert.Contains("<td class=\"small am-po-class-cell\">", view);
            StringAssert.Contains("<td class=\"text-end am-po-line-total\">@Html.FormatCurrency(line.UnitCost)</td>", view);
        }
    }
}
