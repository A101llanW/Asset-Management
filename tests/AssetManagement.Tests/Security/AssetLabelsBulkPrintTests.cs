using System;
using System.Linq;
using System.Reflection;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class AssetLabelsBulkPrintTests
    {
        private static CustomAttributeData PermissionAttribute(MethodInfo method)
        {
            return method.GetCustomAttributesData()
                .FirstOrDefault(a => a.Constructor.DeclaringType.Name == "PermissionAuthorizeAttribute");
        }

        private static bool Has(MethodInfo method, string attributeName)
        {
            return method.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType.Name == attributeName);
        }

        [Test]
        public void BulkPrint_RequiresSameAuthorizationAsPrintCandidates()
        {
            var bulkPrint = typeof(AssetLabelsController).GetMethod("BulkPrint");
            var candidates = typeof(AssetScanController).GetMethod("PrintCandidates");
            Assert.IsNotNull(bulkPrint);
            Assert.IsNotNull(candidates);

            foreach (var name in new[] { "AuthorizeAttribute", "TenantAuthorizeAttribute", "PermissionAuthorizeAttribute", "HttpGetAttribute" })
            {
                Assert.IsTrue(Has(candidates, name), "PrintCandidates baseline missing " + name);
                Assert.IsTrue(Has(bulkPrint, name), "BulkPrint missing " + name);
            }

            Assert.AreEqual("Assets.View", PermissionAttribute(bulkPrint).ConstructorArguments[0].Value);
            Assert.AreEqual("Assets.View", PermissionAttribute(candidates).ConstructorArguments[0].Value);
        }

        [Test]
        public void AssetLabelsController_RequiresTenantRoute()
        {
            Assert.IsTrue(typeof(AssetLabelsController).GetCustomAttributesData()
                .Any(a => a.Constructor.DeclaringType.Name == "RequireTenantRouteAttribute"));
        }

        // The test project does not reference System.Web.Mvc, so MVC results are inspected via reflection.
        [Test]
        public void Index_RedirectsToBulkPrint()
        {
            var controller = new AssetLabelsController(null);
            var result = typeof(AssetLabelsController).GetMethod("Index").Invoke(controller, null);
            Assert.IsNotNull(result);
            Assert.AreEqual("RedirectToRouteResult", result.GetType().Name);
            var routeValues = (System.Collections.Generic.IDictionary<string, object>)result.GetType().GetProperty("RouteValues").GetValue(result, null);
            Assert.AreEqual("BulkPrint", routeValues["action"]);
        }

        [Test]
        public void BuildStatusOptions_StartsWithAnyStatus()
        {
            var options = ((System.Collections.IEnumerable)typeof(AssetScanController).GetMethod("BuildStatusOptions").Invoke(null, null))
                .Cast<object>().ToList();
            Assert.IsTrue(options.Count > 1);
            Assert.AreEqual("Any status", options[0].GetType().GetProperty("Text").GetValue(options[0], null));
        }
    }
}
