using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetManagement.Tests.Requisitions;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>QA 2026-10-28 findings 3-6 and the platform-layout cache-buster (Allan 09:33 EAT): view / script / CSS side.</summary>
    [TestFixture]
    public class QaSixFileRuleTests
    {
        private const string Html2PdfSha256 = "85e6ee9ce246e3ae4424313f7e46a5ed860a28d757811de8dc9c43f306049d65";

        [Test]
        public void Html2Pdf_IsVendored_Verbatim_WithItsLicenses()
        {
            var path = Path("src", "AssetManagement.Web", "Scripts", "vendor", "html2pdf.bundle.min.js");
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2")).ToArray());
                Assert.AreEqual(Html2PdfSha256, hash, "html2pdf.js 0.10.1 exactly as published on cdnjs / npm");
            }

            StringAssert.StartsWith("/*! For license information please see html2pdf.bundle.min.js.LICENSE.txt */", File.ReadAllText(path));
            StringAssert.Contains("html2pdf.js v0.10.1", QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Scripts", "vendor", "html2pdf.bundle.min.js.LICENSE.txt"));
            StringAssert.Contains("Copyright (c) 2017 Erik Koopmans", QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Scripts", "vendor", "html2pdf.LICENSE.txt"));

            var csproj = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "AssetManagement.Web.csproj");
            StringAssert.Contains("<Content Include=\"Scripts\\vendor\\html2pdf.bundle.min.js\" />", csproj);
            StringAssert.Contains("<Content Include=\"Scripts\\vendor\\html2pdf.bundle.min.js.LICENSE.txt\" />", csproj);
            StringAssert.Contains("<Content Include=\"Scripts\\vendor\\html2pdf.LICENSE.txt\" />", csproj);
        }

        [TestCase("PurchaseRequests", "Details.cshtml")]
        [TestCase("Reports", "Index.cshtml")]
        public void PdfViews_LoadTheVendoredHtml2Pdf_WithACacheBuster_AndNoCdn(string folder, string file)
        {
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", folder, file);
            Assert.IsFalse(view.Contains("cdnjs"), "no CDN dependency");
            Assert.IsFalse(view.Contains("https://"), "no external script");
            StringAssert.Contains("<script src=\"@Url.Content(\"~/Scripts/vendor/html2pdf.bundle.min.js\")?v=@System.IO.File.GetLastWriteTimeUtc(Server.MapPath(\"~/Scripts/vendor/html2pdf.bundle.min.js\")).Ticks\"></script>", view);
            StringAssert.Contains("~/Scripts/app/document-pdf.js\")?v=", view);
        }

        [Test]
        public void NoView_LoadsScriptsFromCdnjs()
        {
            var views = Path("src", "AssetManagement.Web");
            var offenders = Directory.GetFiles(views, "*.cshtml", SearchOption.AllDirectories)
                .Where(x => x.IndexOf(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .Where(x => File.ReadAllText(x).IndexOf("cdnjs.cloudflare.com", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void PlatformLayout_CacheBustsSiteInteractive_LikeTheMainLayout()
        {
            const string tag = "<script src=\"@Url.Content(\"~/Scripts/app/site-interactive.js\")?v=@System.IO.File.GetLastWriteTimeUtc(Server.MapPath(\"~/Scripts/app/site-interactive.js\")).Ticks\"></script>";
            StringAssert.Contains(tag, QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Shared", "_Layout.cshtml"));
            var platform = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Areas", "Platform", "Views", "Shared", "_PlatformLayout.cshtml");
            StringAssert.Contains(tag, platform);
            Assert.IsFalse(platform.Contains("site-interactive.js\")\"></script>"), "no un-busted copy left");
        }

        [Test]
        public void ReceiveView_HelpText_SaysTheDefaultIsTheRequestingDepartment_AndCanBeChanged()
        {
            // QA 2026-10-28 finding 5 (Allan: "Keep the department default and reword the help text").
            var view = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Views", "Purchases", "Receive.cshtml");
            StringAssert.Contains("Default: the requesting department (from the requisition; Store if the requisition has no department).", view);
            StringAssert.Contains("You can change it to Store (company custody, no department) or to another department / room.", view);
            Assert.IsFalse(view.Contains("Default: Store (company custody), no department."), "the old Store-default wording is gone");
        }

        internal static string Path(params string[] parts)
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, "src", "AssetManagement.Web")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Repository root not found.");
            }

            return System.IO.Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
        }
    }
}
