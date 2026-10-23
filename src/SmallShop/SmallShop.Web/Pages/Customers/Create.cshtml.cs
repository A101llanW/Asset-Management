using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Customers;

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
            var customer = await _shop.AddCustomerAsync(Input.Name, Input.Phone);
            TempData["Message"] = customer.Name + " added.";
            return RedirectToPage("Details", new { id = customer.Id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Give the customer a name.")]
        [StringLength(120)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "A phone number is required.")]
        [StringLength(40)]
        public string Phone { get; set; } = "";
    }
}
