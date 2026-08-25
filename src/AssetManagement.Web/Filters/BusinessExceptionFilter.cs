using System;
using System.Web.Mvc;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Web.Filters
{
    public class BusinessExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled)
            {
                return;
            }

            var businessException = filterContext.Exception as BusinessException;
            if (businessException == null)
            {
                return;
            }

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;

            if (WantsJsonResponse(filterContext))
            {
                filterContext.HttpContext.Response.StatusCode = 400;
                filterContext.HttpContext.Response.ContentType = "application/json";
                filterContext.Result = new JsonResult
                {
                    Data = new { error = businessException.Message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            if (filterContext.Controller != null)
            {
                filterContext.Controller.TempData["Error"] = businessException.Message;
            }

            var referrer = filterContext.HttpContext.Request.UrlReferrer;
            if (referrer != null)
            {
                filterContext.Result = new RedirectResult(referrer.ToString());
                return;
            }

            filterContext.Result = new RedirectToRouteResult(
                "Default",
                new System.Web.Routing.RouteValueDictionary(new { controller = "Dashboard", action = "Index" }));
        }

        private static bool WantsJsonResponse(ExceptionContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            if (request.IsAjaxRequest())
            {
                return true;
            }

            var path = request.Path ?? string.Empty;
            if (path.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var accept = request.Headers["Accept"];
            return accept != null
                && accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
