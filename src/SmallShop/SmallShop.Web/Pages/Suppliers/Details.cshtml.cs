using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Suppliers;

public class DetailsModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public DetailsModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    public Supplier? Supplier { get; private set; }
    public IReadOnlyList<SupplierGood> Links { get; private set; } = Array.Empty<SupplierGood>();
    public IReadOnlyList<Good> AvailableGoods { get; private set; } = Array.Empty<Good>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddGoodAsync(int id, int goodId, decimal supplyPrice)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }

        try
        {
            await _shop.LinkGoodAsync(id, goodId, supplyPrice);
            TempData["Message"] = "Good added to this supplier.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSetPriceAsync(int id, int goodId, decimal supplyPrice)
    {
        if (!await LoadAsync(id))
        {
            return NotFound();
        }

        try
        {
            await _shop.SetSupplyPriceAsync(id, goodId, supplyPrice);
            TempData["Message"] = "Supply price updated.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int id)
    {
        Supplier = await _db.Suppliers.FirstOrDefaultAsync(item => item.Id == id);
        if (Supplier == null)
        {
            return false;
        }

        Links = await _db.SupplierGoods
            .Include(link => link.Good)
            .Where(link => link.SupplierId == id)
            .OrderBy(link => link.Good.Name)
            .ToListAsync();
        var linkedIds = Links.Select(link => link.GoodId).ToList();
        AvailableGoods = await _db.Goods
            .Where(good => !linkedIds.Contains(good.Id))
            .OrderBy(good => good.Name)
            .ToListAsync();
        return true;
    }
}
