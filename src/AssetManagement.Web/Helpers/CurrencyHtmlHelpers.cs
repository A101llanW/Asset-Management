using System.Web.Mvc;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Helpers
{
    public static class CurrencyHtmlHelpers
    {
        public static MvcHtmlString FormatCurrency(this HtmlHelper html, decimal amount, int decimalPlaces)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount, decimalPlaces));
        }

        public static MvcHtmlString FormatCurrency(this HtmlHelper html, decimal amount)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount));
        }

        public static MvcHtmlString FormatCurrency(this HtmlHelper html, decimal? amount, int decimalPlaces)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount, decimalPlaces));
        }

        public static MvcHtmlString FormatCurrency(this HtmlHelper html, decimal? amount)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount));
        }

        public static MvcHtmlString FormatCurrency<TModel>(this HtmlHelper<TModel> html, decimal amount, int decimalPlaces)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount, decimalPlaces));
        }

        public static MvcHtmlString FormatCurrency<TModel>(this HtmlHelper<TModel> html, decimal amount)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount));
        }

        public static MvcHtmlString FormatCurrency<TModel>(this HtmlHelper<TModel> html, decimal? amount, int decimalPlaces)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount, decimalPlaces));
        }

        public static MvcHtmlString FormatCurrency<TModel>(this HtmlHelper<TModel> html, decimal? amount)
        {
            return MvcHtmlString.Create(CurrencyFormatter.Format(amount));
        }
    }
}
