using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class DepartmentSelectHtmlHelpers
    {
        public static IHtmlString GroupedDepartmentDropDown(
            this HtmlHelper html,
            string name,
            IList<DepartmentSelectGroupVm> groups,
            string optionLabel,
            object htmlAttributes)
        {
            var tag = new TagBuilder("select");
            tag.MergeAttribute("name", name);
            tag.MergeAttribute("id", (name ?? string.Empty).Replace(".", "_").Replace("[", "_").Replace("]", "_"));

            if (htmlAttributes != null)
            {
                tag.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            }

            var body = new StringBuilder();
            if (!string.IsNullOrEmpty(optionLabel))
            {
                body.Append("<option value=\"\">");
                body.Append(HttpUtility.HtmlEncode(optionLabel));
                body.Append("</option>");
            }

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null || group.Items == null || group.Items.Count == 0)
                    {
                        continue;
                    }

                    body.Append("<optgroup label=\"");
                    body.Append(HttpUtility.HtmlEncode(group.Label ?? string.Empty));
                    body.Append("\">");
                    foreach (var item in group.Items)
                    {
                        body.Append("<option value=\"");
                        body.Append(HttpUtility.HtmlEncode(item.Value ?? string.Empty));
                        body.Append("\"");
                        if (item.Selected)
                        {
                            body.Append(" selected=\"selected\"");
                        }

                        body.Append(">");
                        body.Append(HttpUtility.HtmlEncode(item.Text ?? string.Empty));
                        body.Append("</option>");
                    }

                    body.Append("</optgroup>");
                }
            }

            tag.InnerHtml = body.ToString();
            return new HtmlString(tag.ToString());
        }
    }
}
