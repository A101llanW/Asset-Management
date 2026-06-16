using System;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class SidebarNavHtmlHelpers
    {
        public static string SidebarLinkClass(this HtmlHelper html, string controller)
        {
            return LinkClass(html.ViewContext, controller);
        }

        public static string SidebarLinkClassForAction(this HtmlHelper html, string controller, string action)
        {
            return LinkClass(html.ViewContext, controller, action);
        }

        public static string SidebarLinkClassForControllers(this HtmlHelper html, string controller, string alternateController)
        {
            return LinkClassEither(html.ViewContext, controller, alternateController);
        }

        public static string LinkClass(ViewContext viewContext, string controller)
        {
            var currentController = GetControllerName(viewContext);
            return string.Equals(currentController, controller, StringComparison.Ordinal) ? "active" : string.Empty;
        }

        public static string LinkClass(ViewContext viewContext, string controller, string action)
        {
            var currentController = GetControllerName(viewContext);
            var currentAction = GetActionName(viewContext);
            return string.Equals(currentController, controller, StringComparison.Ordinal)
                && string.Equals(currentAction, action, StringComparison.Ordinal)
                ? "active"
                : string.Empty;
        }

        public static string LinkClassEither(ViewContext viewContext, string controller, string alternateController)
        {
            var currentController = GetControllerName(viewContext);
            return string.Equals(currentController, controller, StringComparison.Ordinal)
                || string.Equals(currentController, alternateController, StringComparison.Ordinal)
                ? "active"
                : string.Empty;
        }

        private static string GetControllerName(ViewContext viewContext)
        {
            return viewContext.RouteData.Values["controller"] != null
                ? viewContext.RouteData.Values["controller"].ToString()
                : string.Empty;
        }

        private static string GetActionName(ViewContext viewContext)
        {
            return viewContext.RouteData.Values["action"] as string ?? string.Empty;
        }
    }
}
