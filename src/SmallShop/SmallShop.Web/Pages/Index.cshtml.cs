using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ShopDbContext _db;

    public IndexModel(ShopDbContext db)
    {
        _db = db;
    }

    public int GoodCount { get; private set; }
    public int LowCount { get; private set; }
    public int CustomerCount { get; private set; }
    public int DebtorCount { get; private set; }
    public decimal TotalDebt { get; private set; }
    public int SupplierCount { get; private set; }
    public IReadOnlyList<Good> LowStock { get; private set; } = Array.Empty<Good>();
    public IReadOnlyList<Customer> Debtors { get; private set; } = Array.Empty<Customer>();

    public async Task OnGetAsync()
    {
        GoodCount = await _db.Goods.CountAsync();
        LowCount = await _db.Goods.CountAsync(good => good.QuantityOnHand <= good.LowStockThreshold);
        CustomerCount = await _db.Customers.CountAsync();
        DebtorCount = await _db.Customers.CountAsync(customer => customer.Balance > 0);
        TotalDebt = await _db.Customers.SumAsync(customer => (decimal?)customer.Balance) ?? 0m;
        SupplierCount = await _db.Suppliers.CountAsync();
        LowStock = await _db.Goods
            .Where(good => good.QuantityOnHand <= good.LowStockThreshold)
            .OrderBy(good => good.QuantityOnHand)
            .ThenBy(good => good.Name)
            .Take(5)
            .ToListAsync();
        Debtors = await _db.Customers
            .Where(customer => customer.Balance > 0)
            .OrderByDescending(customer => customer.Balance)
            .ThenBy(customer => customer.Name)
            .Take(5)
            .ToListAsync();
    }
}
