using System;
using System.Linq.Expressions;
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace AssetManagement.Web.Helpers
{
    public static class PasswordFieldExtensions
    {
        public static MvcHtmlString PasswordFieldFor<TModel>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, string>> expression,
            object htmlAttributes = null)
        {
            var attributes = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);
            object cssClass;
            if (attributes.TryGetValue("class", out cssClass) && cssClass != null)
            {
                attributes["class"] = cssClass + " am-password-field";
            }
            else
            {
                attributes["class"] = "form-control am-password-field";
            }

            var field = html.PasswordFor(expression, attributes);
            const string toggleButton =
                "<button type=\"button\" class=\"btn btn-outline-secondary am-password-toggle\" aria-label=\"Show password\" aria-pressed=\"false\">Show</button>";

            return new MvcHtmlString("<div class=\"input-group am-password-input\">" + field + toggleButton + "</div>");
        }
    }
}
