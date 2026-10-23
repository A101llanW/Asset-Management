using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Goods;

public class CreateModel : PageModel
{
    private readonly ShopService _shop;

    public CreateModel(ShopService shop)
    {
        _shop = shop;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var good = await _shop.AddGoodAsync(
                Input.Name,
                Input.Unit,
                Input.Cost,
                Input.SellingPrice,
                Input.OpeningQuantity,
                Input.LowStockThreshold);
            TempData["Message"] = "Opening stock set for " + good.Name + ".";
            return RedirectToPage("Details", new { id = good.Id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Give the good a name.")]
        [StringLength(120)]
        public string Name { get; set; } = "";

        public UnitOfMeasure Unit { get; set; } = UnitOfMeasure.Piece;

        [Range(0, 100000000)]
        public decimal Cost { get; set; }

        [Range(0, 100000000)]
        public decimal SellingPrice { get; set; }

        [Range(0, 100000000)]
        public decimal OpeningQuantity { get; set; }

        [Range(0, 100000000)]
        public decimal LowStockThreshold { get; set; } = 5;
    }
}
