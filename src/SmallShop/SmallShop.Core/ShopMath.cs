namespace SmallShop.Core;

public static class ShopMath
{
    public static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal Qty(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed class ShopRuleException : Exception
{
    public ShopRuleException(string message) : base(message)
    {
    }
}
