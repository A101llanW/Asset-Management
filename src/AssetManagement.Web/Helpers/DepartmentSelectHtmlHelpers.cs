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
                tag.MergeAttributes(new RouteValueDictionary(htmlAttributes), true);
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
                    body.Append(HttpUtility.HtmlEncode(AssetManagement.Application.Helpers.GradeDisplay.ToYear(group.Label ?? string.Empty)));
                    body.Append("\"");
                    if (!string.IsNullOrWhiteSpace(group.CssClass))
                    {
                        body.Append(" class=\"");
                        body.Append(HttpUtility.HtmlAttributeEncode(group.CssClass.Trim()));
                        body.Append("\"");
                    }

                    body.Append(">");
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
                        body.Append(HttpUtility.HtmlEncode(AssetManagement.Application.Helpers.GradeDisplay.ToYear(item.Text ?? string.Empty)));
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
