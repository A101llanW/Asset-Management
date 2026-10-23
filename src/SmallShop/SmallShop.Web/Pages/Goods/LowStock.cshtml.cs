using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class LowStockModel : PageModel
{
    private readonly ShopDbContext _db;

    public LowStockModel(ShopDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Good> Goods { get; private set; } = Array.Empty<Good>();
    public Paging Paging { get; private set; } = new();

    public async Task OnGetAsync(int? take)
    {
        var window = Paging.Normalize(take);
        var query = _db.Goods
            .Where(good => good.QuantityOnHand <= good.LowStockThreshold)
            .OrderBy(good => good.QuantityOnHand)
            .ThenBy(good => good.Name);
        var total = await query.CountAsync();
        Goods = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total };
    }
}
