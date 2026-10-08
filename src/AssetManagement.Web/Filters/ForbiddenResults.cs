using System;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// In-app 403: renders ~/Views/Home/Forbidden.cshtml inside the normal layout with HTTP status 403 and
    /// Response.TrySkipIisCustomErrors = true, so IIS httpErrors never swaps it for the bare
    /// "IIS 10.0 Detailed Error - 403.0" page (a bodiless HttpStatusCodeResult(403) does get swapped).
    /// </summary>
    public class ForbiddenViewResult : ViewResult
    {
        public const string MessageKey = "ForbiddenMessage";

        public ForbiddenViewResult(string message)
        {
            ViewName = "~/Views/Home/Forbidden.cshtml";
            Message = message;
        }

        public string Message { get; private set; }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var response = context.HttpContext.Response;
            response.StatusCode = 403;
            response.TrySkipIisCustomErrors = true;
            if (!string.IsNullOrWhiteSpace(Message))
            {
                ViewData[MessageKey] = Message;
            }

            base.ExecuteResult(context);
        }
    }

    /// <summary>
    /// One place to build a permission denial: the friendly Forbidden page for browser requests, a plain 403
    /// (still TrySkipIisCustomErrors) for AJAX/JSON callers that parse the status code.
    /// </summary>
    public static class ForbiddenResults
    {
        public const string DefaultMessage = "You do not have permission to access this action.";

        public static ActionResult Create(ControllerContext context, string message = null)
        {
            var text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
            var http = context == null ? null : context.HttpContext;
            if (http != null && http.Response != null)
            {
                http.Response.TrySkipIisCustomErrors = true;
            }

            if (http != null && WantsNonHtml(http.Request))
            {
                return new HttpStatusCodeResult(403, text);
            }

            var result = new ForbiddenViewResult(text);
            if (context != null && context.Controller != null)
            {
                result.TempData = context.Controller.TempData;
            }

            return result;
        }

        public static bool WantsNonHtml(HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.IsAjaxRequest())
            {
                return true;
            }

            var accept = request.Headers == null ? null : request.Headers["Accept"];
            return !string.IsNullOrEmpty(accept)
                && accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0
                && accept.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
