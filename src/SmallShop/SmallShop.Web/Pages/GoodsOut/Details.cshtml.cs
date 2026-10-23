using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Pages.GoodsOut;

public class DetailsModel : PageModel
{
    private readonly ShopDbContext _db;

    public DetailsModel(ShopDbContext db)
    {
        _db = db;
    }

    public GoodsOutRecord? Record { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Record = await _db.GoodsOutRecords
            .Include(record => record.Customer)
            .Include(record => record.Lines)
            .ThenInclude(line => line.Good)
            .FirstOrDefaultAsync(record => record.Id == id);
        if (Record == null)
        {
            return NotFound();
        }

        return Page();
    }
}
