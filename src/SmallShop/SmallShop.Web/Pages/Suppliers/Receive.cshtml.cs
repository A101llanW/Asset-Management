using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Suppliers;

public class ReceiveModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public ReceiveModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    public Supplier? Supplier { get; private set; }

    [BindProperty]
    public List<LineInput> Lines { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id, resetLines: true))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id, resetLines: false))
        {
            return NotFound();
        }

        try
        {
            await _shop.ReceiveAsync(new ReceiveRequest
            {
                SupplierId = id,
                Lines = Lines.Select(line => new ReceiveLineRequest
                {
                    GoodId = line.GoodId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    UpdateCost = line.UpdateCost
                }).ToList()
            });
            TempData["Message"] = "Delivery recorded. Stock is up.";
            return RedirectToPage("Details", new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id, resetLines: false);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int id, bool resetLines)
    {
        Supplier = await _db.Suppliers.FirstOrDefaultAsync(item => item.Id == id);
        if (Supplier == null)
        {
            return false;
        }

        var links = await _db.SupplierGoods
            .Include(link => link.Good)
            .Where(link => link.SupplierId == id)
            .OrderBy(link => link.Good.Name)
            .ToListAsync();

        if (resetLines)
        {
            Lines = links.Select(link => new LineInput
            {
                GoodId = link.GoodId,
                Name = link.Good.Name,
                UnitLabel = ShopLabels.Unit(link.Good.Unit),
                OnHand = link.Good.QuantityOnHand,
                CurrentCost = link.Good.Cost,
                UnitCost = link.SupplyPrice,
                UpdateCost = true
            }).ToList();
            return true;
        }

        var byId = links.ToDictionary(link => link.GoodId);
        foreach (var line in Lines)
        {
            if (!byId.TryGetValue(line.GoodId, out var link))
            {
                continue;
            }

            line.Name = link.Good.Name;
            line.UnitLabel = ShopLabels.Unit(link.Good.Unit);
            line.OnHand = link.Good.QuantityOnHand;
            line.CurrentCost = link.Good.Cost;
        }

        return true;
    }

    public class LineInput
    {
        public int GoodId { get; set; }
        public string Name { get; set; } = "";
        public string UnitLabel { get; set; } = "";
        public decimal OnHand { get; set; }
        public decimal CurrentCost { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public bool UpdateCost { get; set; } = true;
    }
}
