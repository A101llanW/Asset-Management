using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Tests;

public sealed class ShopRuleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ShopDbContext _db;
    private readonly ShopService _shop;

    public ShopRuleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ShopDbContext(options);
        _db.Database.EnsureCreated();
        _shop = new ShopService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Credit_goods_out_decreases_stock_and_increases_debt()
    {
        var good = await _shop.AddGoodAsync("Sugar", UnitOfMeasure.Kg, 100m, 150m, 10m, 2m);
        var customer = await _shop.AddCustomerAsync("Amina", "0712000001");

        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = customer.Id,
            AmountPaid = 50m,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 150m }
            }
        });

        Assert.Equal(8m, await QuantityAsync(good.Id));
        Assert.Equal(250m, await BalanceAsync(customer.Id));

        var movement = await _db.StockMovements.SingleAsync(item => item.Kind == StockMovementKind.GoodsOut);
        Assert.Equal(-2m, movement.QuantityDelta);

        var charge = await _db.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryKind.Charge, charge.Kind);
        Assert.Equal(250m, charge.Amount);
        Assert.Equal(250m, charge.BalanceAfter);
    }

    [Fact]
    public async Task Payment_reduces_debt()
    {
        var good = await _shop.AddGoodAsync("Rice", UnitOfMeasure.Kg, 80m, 100m, 5m, 1m);
        var customer = await _shop.AddCustomerAsync("Joseph", "0722000002");
        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = customer.Id,
            AmountPaid = 0m,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 100m }
            }
        });

        await _shop.RecordPaymentAsync(customer.Id, 40m, PaymentMethod.Cash);
        Assert.Equal(160m, await BalanceAsync(customer.Id));

        await _shop.RecordPaymentAsync(customer.Id, 160m, PaymentMethod.Mpesa);
        Assert.Equal(0m, await BalanceAsync(customer.Id));

        var customerRow = await _db.Customers.SingleAsync(item => item.Id == customer.Id);
        Assert.Null(customerRow.UnpaidSince);

        var payments = await _db.LedgerEntries.Where(item => item.Kind == LedgerEntryKind.Payment).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Contains(payments, item => item.Amount == -40m && item.PaymentMethod == PaymentMethod.Cash);
        Assert.Contains(payments, item => item.Amount == -160m && item.PaymentMethod == PaymentMethod.Mpesa);
    }

    [Fact]
    public async Task Payment_cannot_exceed_the_balance()
    {
        var good = await _shop.AddGoodAsync("Soap", UnitOfMeasure.Piece, 30m, 45m, 4m, 1m);
        var customer = await _shop.AddCustomerAsync("Peter", "0733000003");
        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = customer.Id,
            AmountPaid = 20m,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 45m }
            }
        });

        Assert.Equal(70m, await BalanceAsync(customer.Id));

        var ex = await Assert.ThrowsAsync<ShopRuleException>(() =>
            _shop.RecordPaymentAsync(customer.Id, 70.01m, PaymentMethod.Cash));
        Assert.Contains("cannot exceed the balance", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(70m, await BalanceAsync(customer.Id));
        Assert.Equal(2m, await QuantityAsync(good.Id));
    }

    [Fact]
    public async Task Walk_in_goods_out_cannot_create_debt()
    {
        var good = await _shop.AddGoodAsync("Bread", UnitOfMeasure.Piece, 40m, 55m, 10m, 2m);
        var customer = await _shop.AddCustomerAsync("Grace", "0700000004");
        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = customer.Id,
            AmountPaid = 0m,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 1m, UnitPrice = 55m }
            }
        });
        Assert.Equal(55m, await BalanceAsync(customer.Id));

        var ex = await Assert.ThrowsAsync<ShopRuleException>(() =>
            _shop.RecordGoodsOutAsync(new GoodsOutRequest
            {
                CustomerId = null,
                AmountPaid = 10m,
                Lines = new[]
                {
                    new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 55m }
                }
            }));
        Assert.Contains("cannot create debt", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(9m, await QuantityAsync(good.Id));
        Assert.Equal(55m, await BalanceAsync(customer.Id));
        Assert.Equal(0, await _db.GoodsOutRecords.CountAsync(item => item.CustomerId == null));
        Assert.Equal(55m, await _db.LedgerEntries.SumAsync(item => item.Amount));

        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            AmountPaid = 110m,
            TenderMethod = PaymentMethod.Cash,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 55m }
            }
        });

        Assert.Equal(7m, await QuantityAsync(good.Id));
        Assert.Equal(55m, await BalanceAsync(customer.Id));
        Assert.Equal(55m, await _db.LedgerEntries.SumAsync(item => item.Amount));
        var walkIn = await _db.GoodsOutRecords.SingleAsync(item => item.CustomerId == null);
        Assert.Equal(0m, walkIn.AmountCharged);
        Assert.Equal(110m, walkIn.Total);
    }

    [Fact]
    public async Task Return_restores_stock_and_reduces_debt()
    {
        var good = await _shop.AddGoodAsync("Milk", UnitOfMeasure.Litre, 50m, 80m, 10m, 2m);
        var customer = await _shop.AddCustomerAsync("Amina", "0712000001");
        await _shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = customer.Id,
            AmountPaid = 0m,
            Lines = new[]
            {
                new GoodsOutLineRequest { GoodId = good.Id, Quantity = 2m, UnitPrice = 80m }
            }
        });

        Assert.Equal(8m, await QuantityAsync(good.Id));
        Assert.Equal(160m, await BalanceAsync(customer.Id));

        await _shop.RecordReturnAsync(new ReturnRequest
        {
            CustomerId = customer.Id,
            GoodId = good.Id,
            Quantity = 1m,
            UnitPrice = 80m
        });

        Assert.Equal(9m, await QuantityAsync(good.Id));
        Assert.Equal(80m, await BalanceAsync(customer.Id));

        var returned = await _db.StockMovements.SingleAsync(item => item.Kind == StockMovementKind.Return);
        Assert.Equal(1m, returned.QuantityDelta);
        Assert.Equal(9m, returned.QuantityAfter);

        var entry = await _db.LedgerEntries.SingleAsync(item => item.Kind == LedgerEntryKind.Return);
        Assert.Equal(-80m, entry.Amount);
        Assert.Equal(80m, entry.BalanceAfter);
    }

    [Fact]
    public async Task Receiving_from_a_supplier_increases_stock_and_can_update_cost()
    {
        var good = await _shop.AddGoodAsync("Cooking oil", UnitOfMeasure.Litre, 40m, 70m, 10m, 2m);
        var supplier = await _shop.AddSupplierAsync("Lake Distributors", "0722200002");
        await _shop.LinkGoodAsync(supplier.Id, good.Id, 40m);

        await _shop.ReceiveAsync(new ReceiveRequest
        {
            SupplierId = supplier.Id,
            Lines = new[]
            {
                new ReceiveLineRequest
                {
                    GoodId = good.Id,
                    Quantity = 4m,
                    UnitCost = 55m,
                    UpdateCost = true
                }
            }
        });

        Assert.Equal(14m, await QuantityAsync(good.Id));
        Assert.Equal(55m, await CostAsync(good.Id));

        await _shop.ReceiveAsync(new ReceiveRequest
        {
            SupplierId = supplier.Id,
            Lines = new[]
            {
                new ReceiveLineRequest
                {
                    GoodId = good.Id,
                    Quantity = 3m,
                    UnitCost = 70m,
                    UpdateCost = false
                }
            }
        });

        Assert.Equal(17m, await QuantityAsync(good.Id));
        Assert.Equal(55m, await CostAsync(good.Id));

        var deliveries = await _db.StockMovements
            .Where(item => item.Kind == StockMovementKind.Delivery)
            .OrderBy(item => item.Id)
            .ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.Equal(4m, deliveries[0].QuantityDelta);
        Assert.Equal(55m, deliveries[0].UnitPrice);
        Assert.Equal(3m, deliveries[1].QuantityDelta);
        Assert.Equal(supplier.Id, deliveries[1].SupplierId);
    }

    private async Task<decimal> QuantityAsync(int goodId) =>
        await _db.Goods.Where(item => item.Id == goodId).Select(item => item.QuantityOnHand).SingleAsync();

    private async Task<decimal> CostAsync(int goodId) =>
        await _db.Goods.Where(item => item.Id == goodId).Select(item => item.Cost).SingleAsync();

    private async Task<decimal> BalanceAsync(int customerId) =>
        await _db.Customers.Where(item => item.Id == customerId).Select(item => item.Balance).SingleAsync();
}
