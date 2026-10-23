using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class EditModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public EditModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == id);
        if (good == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = good.Id,
            Name = good.Name,
            Unit = good.Unit,
            Cost = good.Cost,
            SellingPrice = good.SellingPrice,
            LowStockThreshold = good.LowStockThreshold
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _shop.UpdateGoodAsync(
                Input.Id,
                Input.Name,
                Input.Unit,
                Input.Cost,
                Input.SellingPrice,
                Input.LowStockThreshold);
            TempData["Message"] = "Good updated. Quantity on hand is changed from stock movements, not here.";
            return RedirectToPage("Details", new { id = Input.Id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    public class InputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Give the good a name.")]
        [StringLength(120)]
        public string Name { get; set; } = "";

        public UnitOfMeasure Unit { get; set; }

        [Range(0, 100000000)]
        public decimal Cost { get; set; }

        [Range(0, 100000000)]
        public decimal SellingPrice { get; set; }

        [Range(0, 100000000)]
        public decimal LowStockThreshold { get; set; }
    }
}
