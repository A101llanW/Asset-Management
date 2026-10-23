namespace SmallShop.Web;

public sealed class Paging
{
    public const int PageSize = 10;

    public int Take { get; init; }
    public int Total { get; init; }
    public int? Id { get; init; }

    public int Shown => Math.Min(Take, Total);
    public int Remaining => Math.Max(0, Total - Shown);
    public bool HasMore => Total > Take;
    public int NextTake => Take + PageSize;

    public static int Normalize(int? take)
    {
        if (take is null || take < PageSize)
        {
            return PageSize;
        }

        if (take > 200)
        {
            return 200;
        }

        return take.Value;
    }
}
