using Microsoft.EntityFrameworkCore;

namespace SmallShop.Core;

public sealed class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options)
    {
    }

    public DbSet<Good> Goods => Set<Good>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierGood> SupplierGoods => Set<SupplierGood>();
    public DbSet<GoodsOutRecord> GoodsOutRecords => Set<GoodsOutRecord>();
    public DbSet<GoodsOutLine> GoodsOutLines => Set<GoodsOutLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Good>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.Property(x => x.SellingPrice).HasPrecision(18, 2);
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            entity.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
            entity.HasMany(x => x.Movements)
                .WithOne(x => x.Good)
                .HasForeignKey(x => x.GoodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.Property(x => x.QuantityDelta).HasPrecision(18, 3);
            entity.Property(x => x.QuantityAfter).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Reason).HasMaxLength(400);
            entity.HasOne(x => x.GoodsOutRecord)
                .WithMany()
                .HasForeignKey(x => x.GoodsOutRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.HasMany(x => x.Entries)
                .WithOne(x => x.Customer)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.Reason).HasMaxLength(400);
            entity.Property(x => x.Note).HasMaxLength(400);
            entity.HasOne(x => x.GoodsOutRecord)
                .WithMany()
                .HasForeignKey(x => x.GoodsOutRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40).IsRequired();
            entity.HasMany(x => x.Goods)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierGood>(entity =>
        {
            entity.Property(x => x.SupplyPrice).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.SupplierId, x.GoodId }).IsUnique();
            entity.HasOne(x => x.Good)
                .WithMany()
                .HasForeignKey(x => x.GoodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsOutRecord>(entity =>
        {
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.Property(x => x.AmountCharged).HasPrecision(18, 2);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines)
                .WithOne(x => x.GoodsOutRecord)
                .HasForeignKey(x => x.GoodsOutRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsOutLine>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.Good)
                .WithMany()
                .HasForeignKey(x => x.GoodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines)
                .WithOne(x => x.GoodsReceipt)
                .HasForeignKey(x => x.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.HasOne(x => x.Good)
                .WithMany()
                .HasForeignKey(x => x.GoodId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
