using System;
using System.Web.Mvc;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    public class AntiForgeryExceptionFilterAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled)
            {
                return;
            }

            if (!(filterContext.Exception is HttpAntiForgeryException))
            {
                return;
            }

            var httpContext = filterContext.HttpContext;
            if (httpContext == null)
            {
                return;
            }

            AuthSessionHelper.SignOut(httpContext);

            filterContext.ExceptionHandled = true;
            httpContext.Response.TrySkipIisCustomErrors = true;

            if (httpContext.Request.IsAjaxRequest())
            {
                httpContext.Response.StatusCode = 401;
                filterContext.Result = new JsonResult
                {
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    Data = new
                    {
                        success = false,
                        reason = "session_expired",
                        message = "Your session has expired. Please sign in and try again."
                    }
                };
                return;
            }

            var controller = filterContext.Controller as Controller;
            if (controller != null)
            {
                controller.TempData["Error"] = "Your session expired. Please sign in and try again.";
            }

            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" },
                    { "reason", "session_expired" }
                });
        }
    }
}
