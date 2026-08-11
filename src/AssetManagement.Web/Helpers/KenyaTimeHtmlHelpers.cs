using System;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Helpers
{
    public static class KenyaTimeHtmlHelpers
    {
        public static MvcHtmlString FormatAuditTimestamp(this HtmlHelper html, DateTime utc)
        {
            return MvcHtmlString.Create(KenyaTimeHelper.FormatUtc(utc));
        }

        public static MvcHtmlString FormatAuditTimestamp(this HtmlHelper html, DateTime utc, string format)
        {
            return MvcHtmlString.Create(KenyaTimeHelper.FormatUtc(utc, format));
        }

        public static MvcHtmlString FormatAuditTimestamp<TModel>(this HtmlHelper<TModel> html, DateTime utc)
        {
            return MvcHtmlString.Create(KenyaTimeHelper.FormatUtc(utc));
        }

        public static MvcHtmlString FormatAuditTimestamp<TModel>(this HtmlHelper<TModel> html, DateTime utc, string format)
        {
            return MvcHtmlString.Create(KenyaTimeHelper.FormatUtc(utc, format));
        }
    }
}
