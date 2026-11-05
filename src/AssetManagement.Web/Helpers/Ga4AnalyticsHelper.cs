using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application.Helpers;
using Newtonsoft.Json;

namespace AssetManagement.Web.Helpers
{
    /// <summary>Everything the GA4 partial needs; already sanitized. Null from <see cref="Ga4AnalyticsHelper.Resolve"/> means GA is off.</summary>
    public sealed class Ga4PageContext
    {
        public string MeasurementId { get; set; }
        public string PagePath { get; set; }
        public string PageTitle { get; set; }
        public string PageReferrer { get; set; }
        public bool AllowNonProductionHost { get; set; }
        public IList<string> ProductionHosts { get; set; }
        public string ConsentStorageKey { get; set; }
    }

    /// <summary>
    /// Reads appSettings Ga4MeasurementId (empty/missing = off) and Ga4AllowNonProductionHost (local test only,
    /// default false) and builds the sanitized page context for the current request.
    /// </summary>
    public static class Ga4AnalyticsHelper
    {
        public const string ConsentStorageKey = "amGa4Consent";

        // Never tracked: hidden SuperAdmin SQL console.
        private static readonly string[] ExcludedControllers = { "DatabaseConsole" };

        private static readonly Lazy<KnownRouteNames> RouteNames = new Lazy<KnownRouteNames>(LoadRouteNames);

        public static Ga4PageContext Resolve(ViewContext viewContext)
        {
            if (viewContext == null || viewContext.HttpContext == null || viewContext.HttpContext.Request == null)
            {
                return null;
            }

            var request = viewContext.HttpContext.Request;
            var measurementId = Ga4AnalyticsRules.NormalizeMeasurementId(ConfigurationManager.AppSettings[Ga4AnalyticsRules.MeasurementIdKey]);
            var allowAnyHost = Ga4AnalyticsRules.ParseFlag(ConfigurationManager.AppSettings[Ga4AnalyticsRules.AllowNonProductionHostKey]);
            var host = request.Url != null ? request.Url.Host : null;
            if (!Ga4AnalyticsRules.ShouldRender(measurementId, host, allowAnyHost))
            {
                return null;
            }

            var routeData = viewContext.RouteData ?? new RouteData();
            var controller = Convert.ToString(routeData.Values["controller"]);
            var action = Convert.ToString(routeData.Values["action"]);
            if (ExcludedControllers.Any(c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var area = Convert.ToString(routeData.DataTokens["area"]);
            var hasTenant = !string.IsNullOrWhiteSpace(Convert.ToString(routeData.Values["tenant"]));
            var id = Convert.ToString(routeData.Values["id"]);
            var appPath = request.ApplicationPath;
            var names = RouteNames.Value;

            Uri referrer = null;
            try
            {
                referrer = request.UrlReferrer;
            }
            catch (UriFormatException)
            {
            }

            return new Ga4PageContext
            {
                MeasurementId = measurementId,
                PagePath = Ga4AnalyticsRules.BuildPagePath(appPath, hasTenant, area, controller, action, id),
                PageTitle = Ga4AnalyticsRules.BuildPageTitle(area, controller, action),
                PageReferrer = Ga4AnalyticsRules.SanitizeReferrer(referrer, host, appPath, names.Controllers, names.Areas),
                AllowNonProductionHost = allowAnyHost,
                ProductionHosts = Ga4AnalyticsRules.ProductionHosts,
                ConsentStorageKey = ConsentStorageKey
            };
        }

        public static IHtmlString ToJson(Ga4PageContext context)
        {
            var json = JsonConvert.SerializeObject(new
            {
                id = context.MeasurementId,
                pagePath = context.PagePath,
                pageTitle = context.PageTitle,
                pageReferrer = context.PageReferrer,
                allowAnyHost = context.AllowNonProductionHost,
                hosts = context.ProductionHosts,
                storageKey = context.ConsentStorageKey
            }, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml });
            return new HtmlString(json);
        }

        private sealed class KnownRouteNames
        {
            public ICollection<string> Controllers { get; set; }
            public ICollection<string> Areas { get; set; }
        }

        private static KnownRouteNames LoadRouteNames()
        {
            var controllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Type[] types;
            try
            {
                types = typeof(Ga4AnalyticsHelper).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var t in types)
            {
                if (t.IsAbstract || !typeof(Controller).IsAssignableFrom(t) || !t.Name.EndsWith("Controller", StringComparison.Ordinal))
                {
                    continue;
                }

                controllers.Add(t.Name.Substring(0, t.Name.Length - "Controller".Length));
                var ns = t.Namespace ?? string.Empty;
                var marker = ns.IndexOf(".Areas.", StringComparison.Ordinal);
                if (marker >= 0)
                {
                    var rest = ns.Substring(marker + ".Areas.".Length);
                    var dot = rest.IndexOf('.');
                    areas.Add(dot >= 0 ? rest.Substring(0, dot) : rest);
                }
            }

            return new KnownRouteNames { Controllers = controllers, Areas = areas };
        }
    }
}
