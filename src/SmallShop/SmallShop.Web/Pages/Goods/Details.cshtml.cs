using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class DetailsModel : PageModel
{
    private readonly ShopDbContext _db;

    public DetailsModel(ShopDbContext db)
    {
        _db = db;
    }

    public Good? Good { get; private set; }
    public IReadOnlyList<StockMovement> Movements { get; private set; } = Array.Empty<StockMovement>();
    public Paging Paging { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, int? take)
    {
        Good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == id);
        if (Good == null)
        {
            return NotFound();
        }

        var window = Paging.Normalize(take);
        var query = _db.StockMovements
            .Where(item => item.GoodId == id)
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id);
        var total = await query.CountAsync();
        Movements = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total, Id = id };
        return Page();
    }
}
