using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    /// <summary>Controller guards and source rules for the Section feature (no printer / Browser Print access).</summary>
    [TestFixture]
    public class DepartmentSectionWebTests
    {
        private static string[] AttributeNames(MethodInfo method)
        {
            return method.GetCustomAttributesData().Select(x => x.Constructor.DeclaringType.Name).ToArray();
        }

        private static string PermissionOf(MethodInfo method)
        {
            var data = method.GetCustomAttributesData().First(x => x.Constructor.DeclaringType.Name == "PermissionAuthorizeAttribute");
            return (string)data.ConstructorArguments[0].Value;
        }

        [TestCase("Create", "Departments.Create")]
        [TestCase("Edit", "Departments.Edit")]
        public void DepartmentsController_PostActions_AreAntiForgeryAndPermissionGuarded(string action, string permission)
        {
            var posts = typeof(DepartmentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == action && AttributeNames(x).Contains("HttpPostAttribute"))
                .ToList();

            Assert.AreEqual(1, posts.Count);
            CollectionAssert.Contains(AttributeNames(posts[0]), "ValidateAntiForgeryTokenAttribute");
            Assert.AreEqual(permission, PermissionOf(posts[0]));
        }

        [Test]
        public void SettingsAssetTagsPost_StaysGuarded()
        {
            var posts = typeof(SettingsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.Name == "AssetTags" && AttributeNames(x).Contains("HttpPostAttribute"))
                .ToList();

            Assert.AreEqual(1, posts.Count);
            CollectionAssert.Contains(AttributeNames(posts[0]), "ValidateAntiForgeryTokenAttribute");
            // Settings.Manage is a class-level guard on SettingsController.
            Assert.IsTrue(typeof(SettingsController).GetCustomAttributesData().Any(x => x.Constructor.DeclaringType.Name == "PermissionAuthorizeAttribute" && (string)x.ConstructorArguments[0].Value == "Settings.Manage"));
        }

        [TestCase("src/AssetManagement.Web/Scripts/app/assets-grouped.js")]
        [TestCase("src/AssetManagement.Web/Scripts/app/reports.js")]
        [TestCase("src/AssetManagement.Web/Views/Departments/Create.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Departments/Edit.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Departments/Index.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Departments/Details.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Assets/Index.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Assets/_AssetRoomGroups.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Settings/AssetTags.cshtml")]
        [TestCase("src/AssetManagement.Web/Views/Reports/_ReportFilters.cshtml")]
        public void SectionFiles_NeverTouchThePrinterOrBrowserPrint(string relative)
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

            Assert.IsFalse(Regex.IsMatch(text, @"9100|9101|BrowserPrint|window\.print", RegexOptions.IgnoreCase), relative);
        }

        [Test]
        public void CreateView_OffersGradeAndClassModesAndTheSectionSelect()
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "AssetManagement.Web", "Views", "Departments", "Create.cshtml"));

            StringAssert.Contains("department-section-select", text);
            StringAssert.Contains("setup-grade-single", text);
            StringAssert.Contains("setup-class-single", text);
        }

        [Test]
        public void AssetRegister_HasASectionFilter_ThatIsCarriedByRedirects()
        {
            var root = RepositoryRoot();
            var index = File.ReadAllText(Path.Combine(root, "src", "AssetManagement.Web", "Views", "Assets", "Index.cshtml"));
            var controller = File.ReadAllText(Path.Combine(root, "src", "AssetManagement.Web", "Controllers", "AssetsController.cs"));

            StringAssert.Contains("amAssetFilterSection", index);
            StringAssert.Contains("DropDownList(\"Section\"", index);
            Assert.GreaterOrEqual(Regex.Matches(controller, @"Section = filter\.Section").Count, 3);
        }

        [Test]
        public void ReportFilters_HaveASectionSelect_ThatTheScriptPosts()
        {
            var root = RepositoryRoot();
            var view = File.ReadAllText(Path.Combine(root, "src", "AssetManagement.Web", "Views", "Reports", "_ReportFilters.cshtml"));
            var script = File.ReadAllText(Path.Combine(root, "src", "AssetManagement.Web", "Scripts", "app", "reports.js"));

            StringAssert.Contains("am-report-section", view);
            StringAssert.Contains("Section", script);
        }

        private static string RepositoryRoot()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CODEXASSET_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(Path.Combine(fromEnvironment, "src", "AssetManagement.Application")))
            {
                return fromEnvironment;
            }

            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Application")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("Repository root not found.");
            return null;
        }
    }
}
