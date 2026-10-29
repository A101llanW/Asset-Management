using System;
using System.IO;
using System.Text.RegularExpressions;
using AssetManagement.Application.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    /// <summary>
    /// Allan, 2026-10-29: the sidebar had two links to the same Departments page ("Departments" ?domain=org and
    /// "Years &amp; Streams" ?domain=classes). One entry remains, "Departments and Classes", gated by Departments.View
    /// (the permission both old links used, so the union is unchanged). Display only: module key stays "Departments".
    /// </summary>
    [TestFixture]
    public class DepartmentsAndClassesMenuTests
    {
        [Test]
        public void Sidebar_HasSingleDepartmentsAndClassesEntry_GatedByDepartmentsView()
        {
            var nav = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "AssetManagement.Web", "Views", "Shared", "_SidebarNav.cshtml"));
            Assert.AreEqual(1, Regex.Matches(nav, ">Departments and Classes</a>").Count);
            StringAssert.DoesNotContain(">Years &amp; Streams<", nav);
            StringAssert.DoesNotContain(">Years & Streams<", nav);
            StringAssert.DoesNotContain("domain = \"classes\"", nav);
            StringAssert.Contains("Departments.View", nav);
        }

        [Test]
        public void ModuleCatalog_DepartmentsModule_KeepsKeyAndPermissions_WithNewDisplayName()
        {
            var module = ModulePermissionCatalog.Find(ModulePermissionCatalog.Departments);
            Assert.IsNotNull(module);
            Assert.AreEqual("Departments", ModulePermissionCatalog.Departments);
            Assert.AreEqual("Departments and Classes", module.DisplayName);
            CollectionAssert.AreEquivalent(
                new[] { "Departments.View", "Departments.Create", "Departments.Edit", "Departments.Delete" },
                ModulePermissionCatalog.PermissionCodesForModule(ModulePermissionCatalog.Departments));
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

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}