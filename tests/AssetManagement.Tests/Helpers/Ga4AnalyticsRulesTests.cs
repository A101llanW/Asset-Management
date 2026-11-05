using System;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class Ga4AnalyticsRulesTests
    {
        private static readonly string[] Controllers = { "Account", "Assets", "Dashboard", "PurchaseRequests", "Reports", "AssetScan", "Organizations" };
        private static readonly string[] Areas = { "Platform" };

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("UA-12345-1")]
        [TestCase("G-")]
        [TestCase("G-<script>")]
        [TestCase("GTM-ABCDEF")]
        public void EmptyOrInvalidMeasurementId_IsOff(string raw)
        {
            Assert.IsNull(Ga4AnalyticsRules.NormalizeMeasurementId(raw));
            Assert.IsFalse(Ga4AnalyticsRules.ShouldRender(raw, "nanosoft.africa", false));
            Assert.IsFalse(Ga4AnalyticsRules.ShouldRender(raw, "localhost", true));
        }

        [Test]
        public void ValidMeasurementId_IsTrimmedAndUpperCased()
        {
            Assert.AreEqual("G-TEST000000", Ga4AnalyticsRules.NormalizeMeasurementId("  g-test000000 "));
        }

        [TestCase("nanosoft.africa", true)]
        [TestCase("www.nanosoft.africa", true)]
        [TestCase("NANOSOFT.AFRICA", true)]
        [TestCase("nanosoft.africa:443", true)]
        [TestCase("localhost", false)]
        [TestCase("127.0.0.1", false)]
        [TestCase("192.168.1.20", false)]
        [TestCase("10.0.0.5", false)]
        [TestCase("alLAN", false)]
        [TestCase("nanosoft.africa.evil.com", false)]
        [TestCase("evilnanosoft.africa", false)]
        [TestCase("test.nanosoft.africa", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void HostGating_ProductionOnly(string host, bool expected)
        {
            Assert.AreEqual(expected, Ga4AnalyticsRules.IsProductionHost(host));
            Assert.AreEqual(expected, Ga4AnalyticsRules.ShouldRender("G-TEST000000", host, false));
        }

        [Test]
        public void LocalTestFlag_AllowsNonProductionHost()
        {
            Assert.IsTrue(Ga4AnalyticsRules.ShouldRender("G-TEST000000", "localhost", true));
            Assert.IsFalse(Ga4AnalyticsRules.ShouldRender("", "localhost", true));
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("false", false)]
        [TestCase("0", false)]
        [TestCase("yes", false)]
        [TestCase("true", true)]
        [TestCase(" TRUE ", true)]
        [TestCase("1", true)]
        public void ParseFlag_DefaultsFalse(string raw, bool expected)
        {
            Assert.AreEqual(expected, Ga4AnalyticsRules.ParseFlag(raw));
        }

        [TestCase("", true, null, "Assets", "Details", "42", "/{org}/Assets/Details/{id}")]
        [TestCase("/NanoAssetSuite", true, null, "Assets", "Details", "42", "/NanoAssetSuite/{org}/Assets/Details/{id}")]
        [TestCase("/NanoAssetSuite/", true, null, "Assets", "Edit", "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "/NanoAssetSuite/{org}/Assets/Edit/{guid}")]
        [TestCase("/", true, null, "AssetScan", "Lookup", "NIS-ICT-0042", "/{org}/AssetScan/Lookup/{tag}")]
        [TestCase("/", true, null, "AssetScan", "Lookup", "LAPTOP", "/{org}/AssetScan/Lookup/{tag}")]
        [TestCase("/", false, "Platform", "Organizations", "Index", null, "/Platform/Organizations/Index")]
        [TestCase("/", false, null, "Account", "Login", "", "/Account/Login")]
        [TestCase("/", true, null, null, null, null, "/{org}/Home/Index")]
        [TestCase("/", true, null, "Assets<x>", "Details", null, "/{org}/{value}/Details")]
        public void BuildPagePath_UsesRouteNamesAndPlaceholders(string appPath, bool hasTenant, string area, string controller, string action, string id, string expected)
        {
            Assert.AreEqual(expected, Ga4AnalyticsRules.BuildPagePath(appPath, hasTenant, area, controller, action, id));
        }

        [Test]
        public void BuildPageTitle_IsGeneric()
        {
            Assert.AreEqual("Assets Details", Ga4AnalyticsRules.BuildPageTitle(null, "Assets", "Details"));
            Assert.AreEqual("Platform Organizations Index", Ga4AnalyticsRules.BuildPageTitle("Platform", "Organizations", "Index"));
        }

        [TestCase("/a46138179/Assets/Details/123", "/{org}/Assets/Details/{id}")]
        [TestCase("/A46138179/Assets/Details/123?tab=history&name=Dell", "/{org}/Assets/Details/{id}")]
        [TestCase("/nis/Assets/Index#top", "/{org}/Assets/Index")]
        [TestCase("/nanosoft", "/{org}")]
        [TestCase("/Assets/Details/9", "/Assets/Details/{id}")]
        [TestCase("/a46138179/Assets/Edit/3F2504E0-4F89-11D3-9A0C-0305E82C3301", "/{org}/Assets/Edit/{guid}")]
        [TestCase("/a46138179/AssetScan/Lookup/NIS-ICT-0042", "/{org}/AssetScan/Lookup/{tag}")]
        [TestCase("/a46138179/AssetScan/Lookup/LAPTOP/extra", "/{org}/AssetScan/Lookup/{tag}/{tag}")]
        [TestCase("/a46138179/Users/Details/john%40school.ac.ke", "/{org}/Users/Details/{tag}")]
        [TestCase("/Platform/Organizations/Edit/13", "/Platform/Organizations/Edit/{id}")]
        [TestCase("https://nanosoft.africa/a46138179/Reports/Index?from=2026-01-01", "/{org}/Reports/Index")]
        [TestCase("", "/")]
        [TestCase(null, "/")]
        public void SanitizePath_StripsIdsTagsOrgAndQuery(string raw, string expected)
        {
            Assert.AreEqual(expected, Ga4AnalyticsRules.SanitizePath(raw, "/", Controllers, Areas));
        }

        [Test]
        public void SanitizePath_StripsApplicationPath()
        {
            Assert.AreEqual("/NanoAssetSuite/{org}/Assets/Details/{id}",
                Ga4AnalyticsRules.SanitizePath("/NanoAssetSuite/a46138179/Assets/Details/5?x=1", "/NanoAssetSuite", Controllers, Areas));
        }

        [Test]
        public void SanitizeReferrer_SameHostSanitized_OtherHostOriginOnly()
        {
            Assert.AreEqual("https://nanosoft.africa/NanoAssetSuite/{org}/Assets/Details/{id}",
                Ga4AnalyticsRules.SanitizeReferrer(new Uri("https://nanosoft.africa/NanoAssetSuite/a46138179/Assets/Details/77?q=Dell"),
                    "nanosoft.africa", "/NanoAssetSuite", Controllers, Areas));
            Assert.AreEqual("https://www.google.com/",
                Ga4AnalyticsRules.SanitizeReferrer(new Uri("https://www.google.com/search?q=nanosoft+asset"), "nanosoft.africa", "/NanoAssetSuite", Controllers, Areas));
            Assert.AreEqual(string.Empty, Ga4AnalyticsRules.SanitizeReferrer(null, "nanosoft.africa", "/", Controllers, Areas));
        }

        [TestCase("asset_create", true)]
        [TestCase("report_export_csv", true)]
        [TestCase("Asset_Create", false)]
        [TestCase("asset create", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void EventNames_AreValidated(string name, bool expected)
        {
            Assert.AreEqual(expected, Ga4AnalyticsRules.IsValidEventName(name));
        }
    }
}
