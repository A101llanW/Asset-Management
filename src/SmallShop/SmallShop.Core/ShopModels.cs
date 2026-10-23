namespace SmallShop.Core;

public enum UnitOfMeasure
{
    Piece = 0,
    Kg = 1,
    Litre = 2
}

public enum StockMovementKind
{
    Opening = 0,
    Delivery = 1,
    Count = 2,
    Damage = 3,
    Theft = 4,
    GoodsOut = 5,
    Return = 6
}

public enum StockAdjustmentKind
{
    Count = 0,
    Damage = 1,
    Theft = 2
}

public enum LedgerEntryKind
{
    Charge = 0,
    Payment = 1,
    Return = 2,
    WriteOff = 3,
    Adjustment = 4
}

public enum PaymentMethod
{
    Cash = 0,
    Mpesa = 1
}

public static class ShopLabels
{
    public static string Unit(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Piece => "piece",
        UnitOfMeasure.Kg => "kg",
        UnitOfMeasure.Litre => "litre",
        _ => unit.ToString()
    };

    public static string Movement(StockMovementKind kind) => kind switch
    {
        StockMovementKind.Opening => "Opening stock",
        StockMovementKind.Delivery => "Delivery",
        StockMovementKind.Count => "Count",
        StockMovementKind.Damage => "Damage",
        StockMovementKind.Theft => "Theft",
        StockMovementKind.GoodsOut => "Goods out",
        StockMovementKind.Return => "Return",
        _ => kind.ToString()
    };

    public static string Ledger(LedgerEntryKind kind) => kind switch
    {
        LedgerEntryKind.Charge => "Goods out",
        LedgerEntryKind.Payment => "Payment",
        LedgerEntryKind.Return => "Return",
        LedgerEntryKind.WriteOff => "Write-off",
        LedgerEntryKind.Adjustment => "Adjustment",
        _ => kind.ToString()
    };

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Mpesa => "M-Pesa",
        _ => method.ToString()
    };
}

public sealed class Good
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public UnitOfMeasure Unit { get; set; }
    public decimal Cost { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal LowStockThreshold { get; set; }
    public List<StockMovement> Movements { get; set; } = new();

    public bool IsLow => QuantityOnHand <= LowStockThreshold;
}

public sealed class StockMovement
{
    public int Id { get; set; }
    public int GoodId { get; set; }
    public Good Good { get; set; } = null!;
    public StockMovementKind Kind { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal QuantityAfter { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int? GoodsOutRecordId { get; set; }
    public GoodsOutRecord? GoodsOutRecord { get; set; }
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime? UnpaidSince { get; set; }
    public List<LedgerEntry> Entries { get; set; } = new();
}

public sealed class LedgerEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public LedgerEntryKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; }
    public int? GoodsOutRecordId { get; set; }
    public GoodsOutRecord? GoodsOutRecord { get; set; }
}

public sealed class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<SupplierGood> Goods { get; set; } = new();
}

public sealed class SupplierGood
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int GoodId { get; set; }
    public Good Good { get; set; } = null!;
    public decimal SupplyPrice { get; set; }
}

public sealed class GoodsOutRecord
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime OccurredAt { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountCharged { get; set; }
    public PaymentMethod? TenderMethod { get; set; }
    public List<GoodsOutLine> Lines { get; set; } = new();
}

public sealed class GoodsOutLine
{
    public int Id { get; set; }
    public int GoodsOutRecordId { get; set; }
    public GoodsOutRecord GoodsOutRecord { get; set; } = null!;
    public int GoodId { get; set; }
    public Good Good { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class GoodsReceipt
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
    public List<GoodsReceiptLine> Lines { get; set; } = new();
}

public sealed class GoodsReceiptLine
{
    public int Id { get; set; }
    public int GoodsReceiptId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public int GoodId { get; set; }
    public Good Good { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public bool UpdatedCost { get; set; }
}
