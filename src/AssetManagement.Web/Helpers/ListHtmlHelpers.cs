using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class ListHtmlHelpers
    {
        public static IHtmlString SortLink(this HtmlHelper html, string label, string sortKey, string currentSort, string currentDirection)
        {
            var request = html.ViewContext.HttpContext.Request;
            var query = HttpUtility.ParseQueryString(request.QueryString.ToString());
            var nextDirection = currentSort == sortKey && string.Equals(currentDirection, "asc", System.StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc";

            query["sort"] = sortKey;
            query["direction"] = nextDirection;
            query["page"] = "1";

            var indicator = currentSort == sortKey
                ? string.Equals(currentDirection, "asc", System.StringComparison.OrdinalIgnoreCase) ? " ↑" : " ↓"
                : string.Empty;
            var url = request.Path + "?" + query;
            var anchor = "<a class=\"am-sort-link\" href=\"" + HttpUtility.HtmlAttributeEncode(url) + "\">"
                         + HttpUtility.HtmlEncode(label + indicator)
                         + "</a>";
            return new MvcHtmlString(anchor);
        }
    }
}
