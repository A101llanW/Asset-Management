using System.Globalization;

namespace SmallShop.Web;

public static class ShopFormat
{
    public static string Money(decimal value) =>
        "KSh " + value.ToString("N2", CultureInfo.InvariantCulture);

    public static string SignedMoney(decimal value)
    {
        if (value > 0)
        {
            return "+" + Money(value);
        }

        if (value < 0)
        {
            return "−" + Money(Math.Abs(value));
        }

        return Money(0);
    }

    public static string Qty(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string SignedQty(decimal value)
    {
        if (value > 0)
        {
            return "+" + Qty(value);
        }

        if (value < 0)
        {
            return "−" + Qty(Math.Abs(value));
        }

        return "0";
    }

    public static string When(DateTime value) =>
        value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string UnpaidFor(DateTime? since, decimal balance)
    {
        if (balance <= 0 || since is null)
        {
            return "—";
        }

        var days = (DateTime.UtcNow.Date - since.Value.Date).Days;
        if (days <= 0)
        {
            return "today";
        }

        if (days == 1)
        {
            return "1 day";
        }

        return days + " days";
    }
}
