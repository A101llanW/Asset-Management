using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.GoodsOut;

public class CreateModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public CreateModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    public IReadOnlyList<Good> Goods { get; private set; } = Array.Empty<Good>();
    public IReadOnlyList<Customer> Customers { get; private set; } = Array.Empty<Customer>();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(int? customerId)
    {
        await LoadListsAsync();
        Input.CustomerId = customerId;
        Input.Lines = BlankLines();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (Input.Lines == null || Input.Lines.Count == 0)
        {
            Input.Lines = BlankLines();
        }

        try
        {
            var record = await _shop.RecordGoodsOutAsync(new GoodsOutRequest
            {
                CustomerId = Input.CustomerId,
                AmountPaid = Input.AmountPaid,
                TenderMethod = Input.TenderMethod,
                Lines = Input.Lines.Select(line => new GoodsOutLineRequest
                {
                    GoodId = line.GoodId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                }).ToList()
            });
            TempData["Message"] = record.AmountCharged > 0
                ? "Goods out recorded. " + ShopFormat.Money(record.AmountCharged) + " added to the customer's balance."
                : "Goods out recorded.";
            return RedirectToPage("Details", new { id = record.Id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task LoadListsAsync()
    {
        Goods = await _db.Goods.OrderBy(good => good.Name).ToListAsync();
        Customers = await _db.Customers.OrderBy(customer => customer.Name).ToListAsync();
    }

    private static List<LineInput> BlankLines()
    {
        var lines = new List<LineInput>();
        for (var i = 0; i < 5; i++)
        {
            lines.Add(new LineInput());
        }

        return lines;
    }

    public class InputModel
    {
        public int? CustomerId { get; set; }
        public decimal AmountPaid { get; set; }
        public PaymentMethod TenderMethod { get; set; } = PaymentMethod.Cash;
        public List<LineInput> Lines { get; set; } = new();
    }

    public class LineInput
    {
        public int GoodId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
    }
}
