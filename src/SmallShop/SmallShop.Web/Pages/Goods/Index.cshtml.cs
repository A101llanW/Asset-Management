using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class IndexModel : PageModel
{
    private readonly ShopDbContext _db;

    public IndexModel(ShopDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Good> Goods { get; private set; } = Array.Empty<Good>();
    public Paging Paging { get; private set; } = new();
    public int LowCount { get; private set; }

    public async Task OnGetAsync(int? take)
    {
        var window = Paging.Normalize(take);
        var query = _db.Goods.OrderBy(good => good.Name);
        var total = await query.CountAsync();
        Goods = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total };
        LowCount = await _db.Goods.CountAsync(good => good.QuantityOnHand <= good.LowStockThreshold);
    }
}
