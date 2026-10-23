using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.GoodsOut;

public class IndexModel : PageModel
{
    private readonly ShopDbContext _db;

    public IndexModel(ShopDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<GoodsOutRecord> Records { get; private set; } = Array.Empty<GoodsOutRecord>();
    public Paging Paging { get; private set; } = new();

    public async Task OnGetAsync(int? take)
    {
        var window = Paging.Normalize(take);
        var query = _db.GoodsOutRecords
            .Include(record => record.Customer)
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id);
        var total = await query.CountAsync();
        Records = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total };
    }
}
