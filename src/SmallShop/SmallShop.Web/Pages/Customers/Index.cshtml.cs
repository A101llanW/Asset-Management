using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly ShopDbContext _db;

    public IndexModel(ShopDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Customer> Customers { get; private set; } = Array.Empty<Customer>();
    public Paging Paging { get; private set; } = new();
    public decimal TotalDebt { get; private set; }

    public async Task OnGetAsync(int? take)
    {
        var window = Paging.Normalize(take);
        var query = _db.Customers
            .OrderByDescending(customer => customer.Balance)
            .ThenBy(customer => customer.Name);
        var total = await query.CountAsync();
        Customers = await query.Take(window).ToListAsync();
        Paging = new Paging { Take = window, Total = total };
        TotalDebt = await _db.Customers.SumAsync(customer => (decimal?)customer.Balance) ?? 0m;
    }
}
