using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// HTML helper overloads not available on all MVC 3 builds (format + htmlAttributes combinations).
    /// Do not duplicate DropDownListFor here - SelectExtensions already provides that overload.
    /// </summary>
    public static class Mvc3HtmlHelpers
    {
        public static MvcHtmlString LabelFor<TModel, TValue>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TValue>> expression,
            object htmlAttributes)
        {
            var fieldName = ExpressionHelper.GetExpressionText(expression);
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var labelText = metadata.DisplayName ?? metadata.PropertyName ?? fieldName;
            var tag = new TagBuilder("label");
            tag.Attributes["for"] = html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldId(fieldName);
            tag.MergeAttributes(HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
            tag.SetInnerText(labelText);
            return MvcHtmlString.Create(tag.ToString());
        }

        public static MvcHtmlString TextBoxFor<TModel, TValue>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TValue>> expression,
            string format,
            object htmlAttributes)
        {
            var fieldName = ExpressionHelper.GetExpressionText(expression);
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var formattedValue = FormatModelValue(metadata.Model, format);
            return html.TextBox(fieldName, formattedValue, htmlAttributes);
        }

        public static MvcHtmlString TextAreaFor<TModel, TValue>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TValue>> expression,
            int rows,
            object htmlAttributes)
        {
            return BuildTextArea(html, expression, rows, htmlAttributes);
        }

        private static MvcHtmlString BuildTextArea<TModel, TValue>(
            HtmlHelper<TModel> html,
            Expression<Func<TModel, TValue>> expression,
            int rows,
            object htmlAttributes)
        {
            var fieldName = ExpressionHelper.GetExpressionText(expression);
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var tag = new TagBuilder("textarea");
            tag.MergeAttributes(HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
            tag.MergeAttribute("rows", rows.ToString(CultureInfo.InvariantCulture), true);
            tag.MergeAttribute("name", fieldName, true);
            tag.MergeAttribute("id", html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldId(fieldName));
            var value = metadata.Model != null ? Convert.ToString(metadata.Model, CultureInfo.CurrentCulture) : string.Empty;
            tag.SetInnerText(value);
            return MvcHtmlString.Create(tag.ToString());
        }

        private static string FormatModelValue(object model, string format)
        {
            if (model == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(format))
            {
                return Convert.ToString(model, CultureInfo.CurrentCulture);
            }

            return string.Format(CultureInfo.CurrentCulture, format, model);
        }
    }
}
