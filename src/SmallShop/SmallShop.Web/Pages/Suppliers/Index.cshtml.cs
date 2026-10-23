using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.Suppliers;

public class IndexModel : PageModel
{
    private readonly ShopDbContext _db;

    public IndexModel(ShopDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<SupplierRow> Suppliers { get; private set; } = Array.Empty<SupplierRow>();
    public Paging Paging { get; private set; } = new();

    public async Task OnGetAsync(int? take)
    {
        var window = Paging.Normalize(take);
        var query = _db.Suppliers.OrderBy(supplier => supplier.Name);
        var total = await query.CountAsync();
        var suppliers = await query.Take(window).ToListAsync();
        var ids = suppliers.Select(supplier => supplier.Id).ToList();
        var counts = await _db.SupplierGoods
            .Where(link => ids.Contains(link.SupplierId))
            .GroupBy(link => link.SupplierId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync();
        var byId = counts.ToDictionary(item => item.Key, item => item.Count);
        Suppliers = suppliers
            .Select(supplier => new SupplierRow
            {
                Supplier = supplier,
                GoodCount = byId.TryGetValue(supplier.Id, out var count) ? count : 0
            })
            .ToList();
        Paging = new Paging { Take = window, Total = total };
    }

    public sealed class SupplierRow
    {
        public Supplier Supplier { get; set; } = null!;
        public int GoodCount { get; set; }
    }
}
