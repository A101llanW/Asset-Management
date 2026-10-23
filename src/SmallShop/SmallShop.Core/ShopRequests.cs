namespace SmallShop.Core;

public sealed class GoodsOutRequest
{
    public int? CustomerId { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod? TenderMethod { get; set; }
    public DateTime? When { get; set; }
    public IReadOnlyList<GoodsOutLineRequest> Lines { get; set; } = Array.Empty<GoodsOutLineRequest>();
}

public sealed class GoodsOutLineRequest
{
    public int GoodId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
}

public sealed class ReturnRequest
{
    public int CustomerId { get; set; }
    public int GoodId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime? When { get; set; }
}

public sealed class ReceiveRequest
{
    public int SupplierId { get; set; }
    public DateTime? When { get; set; }
    public IReadOnlyList<ReceiveLineRequest> Lines { get; set; } = Array.Empty<ReceiveLineRequest>();
}

public sealed class ReceiveLineRequest
{
    public int GoodId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public bool UpdateCost { get; set; }
}
