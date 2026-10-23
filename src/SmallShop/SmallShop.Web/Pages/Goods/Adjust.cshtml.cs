using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class AdjustModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public AdjustModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    public Good? Good { get; private set; }

    [BindProperty]
    public string Kind { get; set; } = "Count";

    [BindProperty]
    public decimal Quantity { get; set; }

    [BindProperty]
    public string? Reason { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == id);
        if (Good == null)
        {
            return NotFound();
        }

        Quantity = Good.QuantityOnHand;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        Good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == id);
        if (Good == null)
        {
            return NotFound();
        }

        try
        {
            if (Kind == "Opening")
            {
                await _shop.SetOpeningStockAsync(id, Quantity);
                TempData["Message"] = "Opening stock now sets the count for " + Good.Name + ".";
            }
            else
            {
                var kind = Kind switch
                {
                    "Damage" => StockAdjustmentKind.Damage,
                    "Theft" => StockAdjustmentKind.Theft,
                    _ => StockAdjustmentKind.Count
                };
                await _shop.AdjustStockAsync(id, kind, Quantity, Reason ?? "");
                TempData["Message"] = "Stock adjusted for " + Good.Name + ".";
            }

            return RedirectToPage("Details", new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == id);
            return Page();
        }
    }
}
