using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Customers;

public class DetailsModel : PageModel
{
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public DetailsModel(ShopDbContext db, ShopService shop)
    {
        _db = db;
        _shop = shop;
    }

    public Customer? Customer { get; private set; }
    public IReadOnlyList<LedgerEntry> Entries { get; private set; } = Array.Empty<LedgerEntry>();
    public IReadOnlyList<Good> ReturnableGoods { get; private set; } = Array.Empty<Good>();
    public Paging Paging { get; private set; } = new();

    [BindProperty]
    public PaymentInput Payment { get; set; } = new();

    [BindProperty]
    public ReturnInput Return { get; set; } = new();

    [BindProperty]
    public ReasonAmountInput WriteOff { get; set; } = new();

    [BindProperty]
    public ReasonAmountInput Adjustment { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, int? take)
    {
        if (!await LoadAsync(id, take))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPaymentAsync(int id, int? take)
    {
        if (!await LoadAsync(id, take))
        {
            return NotFound();
        }

        if (Payment.Amount <= 0)
        {
            ModelState.AddModelError(string.Empty, "Enter a payment amount.");
            return Page();
        }

        try
        {
            await _shop.RecordPaymentAsync(id, Payment.Amount, Payment.Method);
            TempData["Message"] = "Payment recorded.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id, take);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostReturnAsync(int id, int? take)
    {
        if (!await LoadAsync(id, take))
        {
            return NotFound();
        }

        try
        {
            await _shop.RecordReturnAsync(new ReturnRequest
            {
                CustomerId = id,
                GoodId = Return.GoodId,
                Quantity = Return.Quantity,
                UnitPrice = Return.UnitPrice
            });
            TempData["Message"] = "Return recorded. Stock is back and the balance is reduced.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id, take);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostWriteOffAsync(int id, int? take)
    {
        if (!await LoadAsync(id, take))
        {
            return NotFound();
        }

        try
        {
            await _shop.WriteOffAsync(id, WriteOff.Amount, WriteOff.Reason ?? "");
            TempData["Message"] = "Write-off recorded.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id, take);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAdjustAsync(int id, int? take)
    {
        if (!await LoadAsync(id, take))
        {
            return NotFound();
        }

        try
        {
            await _shop.AdjustBalanceAsync(id, Adjustment.Amount, Adjustment.Reason ?? "");
            TempData["Message"] = "Balance adjusted.";
            return RedirectToPage(new { id });
        }
        catch (ShopRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync(id, take);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int id, int? take)
    {
        Customer = await _db.Customers.FirstOrDefaultAsync(item => item.Id == id);
        if (Customer == null)
        {
            return false;
        }

        var window = Paging.Normalize(take);
        var query = _db.LedgerEntries
            .Where(item => item.CustomerId == id)
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id);
        var total = await query.CountAsync();
        Entries = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total, Id = id };

        var goodIds = await _db.GoodsOutLines
            .Where(line => line.GoodsOutRecord.CustomerId == id)
            .Select(line => line.GoodId)
            .Distinct()
            .ToListAsync();
        ReturnableGoods = await _db.Goods
            .Where(good => goodIds.Contains(good.Id))
            .OrderBy(good => good.Name)
            .ToListAsync();
        return true;
    }

    public class PaymentInput
    {
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    }

    public class ReturnInput
    {
        public int GoodId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    public class ReasonAmountInput
    {
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
    }
}
