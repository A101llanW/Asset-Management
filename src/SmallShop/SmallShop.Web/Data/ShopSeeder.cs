using Microsoft.EntityFrameworkCore;
using SmallShop.Core;

namespace SmallShop.Web.Data;

public static class ShopSeeder
{
    public static async Task SeedAsync(ShopDbContext db, ShopService shop, CancellationToken cancellationToken = default)
    {
        if (await db.Goods.AnyAsync(cancellationToken))
        {
            return;
        }

        var opening = DaysAgo(45);
        var goods = new Dictionary<string, Good>(StringComparer.Ordinal);
        foreach (var seed in GoodSeeds)
        {
            goods[seed.Name] = await shop.AddGoodAsync(
                seed.Name,
                seed.Unit,
                seed.Cost,
                seed.Price,
                seed.Opening,
                seed.Low,
                opening,
                cancellationToken);
        }

        var kisumu = await shop.AddSupplierAsync("Kisumu Wholesalers", "0711 100 001", cancellationToken);
        var lake = await shop.AddSupplierAsync("Lake Distributors", "0722 200 002", cancellationToken);
        var town = await shop.AddSupplierAsync("Town Traders", "0733 300 003", cancellationToken);

        await LinkAsync(shop, kisumu.Id, goods, cancellationToken,
            ("Sugar", 115m),
            ("Maize flour", 90m),
            ("Rice", 105m),
            ("Beans", 140m),
            ("Wheat flour", 80m));
        await LinkAsync(shop, lake.Id, goods, cancellationToken,
            ("Cooking oil", 190m),
            ("Milk", 55m),
            ("Drinking water", 22m));
        await LinkAsync(shop, town.Id, goods, cancellationToken,
            ("Bread", 40m),
            ("Tea leaves", 35m),
            ("Salt", 15m),
            ("Soap", 30m),
            ("Eggs (tray)", 280m),
            ("Charcoal", 40m));

        var delivery = DaysAgo(20);
        await shop.ReceiveAsync(new ReceiveRequest
        {
            SupplierId = kisumu.Id,
            When = delivery,
            Lines = new[]
            {
                Line(goods, "Sugar", 20m, 115m, true),
                Line(goods, "Rice", 10m, 105m, true),
                Line(goods, "Beans", 2m, 140m, false)
            }
        }, cancellationToken);
        await shop.ReceiveAsync(new ReceiveRequest
        {
            SupplierId = lake.Id,
            When = delivery,
            Lines = new[]
            {
                Line(goods, "Cooking oil", 12m, 190m, true),
                Line(goods, "Drinking water", 10m, 22m, true)
            }
        }, cancellationToken);
        await shop.ReceiveAsync(new ReceiveRequest
        {
            SupplierId = town.Id,
            When = delivery,
            Lines = new[]
            {
                Line(goods, "Bread", 10m, 40m, false),
                Line(goods, "Soap", 12m, 30m, false)
            }
        }, cancellationToken);

        var customers = new List<Customer>();
        foreach (var seed in CustomerSeeds)
        {
            customers.Add(await shop.AddCustomerAsync(seed.Name, seed.Phone, cancellationToken));
        }

        var byName = customers.ToDictionary(customer => customer.Name, StringComparer.Ordinal);

        await shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = byName["Amina Wanjiku"].Id,
            AmountPaid = 150m,
            TenderMethod = PaymentMethod.Cash,
            When = DaysAgo(18),
            Lines = new[] { Out(goods, "Sugar", 4m, 150m) }
        }, cancellationToken);
        await shop.RecordPaymentAsync(byName["Amina Wanjiku"].Id, 100m, PaymentMethod.Mpesa, DaysAgo(1), cancellationToken);

        await shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = byName["Joseph Otieno"].Id,
            AmountPaid = 0m,
            When = DaysAgo(12),
            Lines = new[] { Out(goods, "Eggs (tray)", 2m, 360m) }
        }, cancellationToken);
        await shop.RecordPaymentAsync(byName["Joseph Otieno"].Id, 200m, PaymentMethod.Cash, DaysAgo(4), cancellationToken);

        await shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = byName["Peter Kamau"].Id,
            AmountPaid = 80m,
            TenderMethod = PaymentMethod.Mpesa,
            When = DaysAgo(7),
            Lines = new[] { Out(goods, "Soap", 4m, 45m) }
        }, cancellationToken);

        await shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            CustomerId = byName["Grace Achieng"].Id,
            AmountPaid = 280m,
            TenderMethod = PaymentMethod.Cash,
            When = DaysAgo(2),
            Lines = new[] { Out(goods, "Rice", 2m, 140m) }
        }, cancellationToken);

        await shop.RecordGoodsOutAsync(new GoodsOutRequest
        {
            AmountPaid = 165m,
            TenderMethod = PaymentMethod.Cash,
            When = DaysAgo(1),
            Lines = new[] { Out(goods, "Bread", 3m, 55m) }
        }, cancellationToken);

        await shop.AdjustStockAsync(
            goods["Charcoal"].Id,
            StockAdjustmentKind.Theft,
            1m,
            "Bag missing after market day",
            DaysAgo(3),
            cancellationToken);
    }

    private static async Task LinkAsync(
        ShopService shop,
        int supplierId,
        IReadOnlyDictionary<string, Good> goods,
        CancellationToken cancellationToken,
        params (string Name, decimal Price)[] links)
    {
        foreach (var link in links)
        {
            await shop.LinkGoodAsync(supplierId, goods[link.Name].Id, link.Price, cancellationToken);
        }
    }

    private static ReceiveLineRequest Line(
        IReadOnlyDictionary<string, Good> goods,
        string name,
        decimal quantity,
        decimal unitCost,
        bool updateCost)
    {
        return new ReceiveLineRequest
        {
            GoodId = goods[name].Id,
            Quantity = quantity,
            UnitCost = unitCost,
            UpdateCost = updateCost
        };
    }

    private static GoodsOutLineRequest Out(
        IReadOnlyDictionary<string, Good> goods,
        string name,
        decimal quantity,
        decimal unitPrice)
    {
        return new GoodsOutLineRequest
        {
            GoodId = goods[name].Id,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }

    private static DateTime DaysAgo(int days) => DateTime.UtcNow.Date.AddDays(-days).AddHours(8);

    private static readonly GoodSeed[] GoodSeeds =
    {
        new("Sugar", UnitOfMeasure.Kg, 110m, 150m, 25m, 10m),
        new("Maize flour", UnitOfMeasure.Kg, 90m, 130m, 8m, 10m),
        new("Cooking oil", UnitOfMeasure.Litre, 200m, 260m, 6m, 8m),
        new("Milk", UnitOfMeasure.Litre, 55m, 70m, 4m, 6m),
        new("Bread", UnitOfMeasure.Piece, 40m, 55m, 18m, 6m),
        new("Rice", UnitOfMeasure.Kg, 100m, 140m, 15m, 8m),
        new("Beans", UnitOfMeasure.Kg, 140m, 190m, 4m, 6m),
        new("Tea leaves", UnitOfMeasure.Piece, 35m, 50m, 7m, 8m),
        new("Salt", UnitOfMeasure.Piece, 15m, 25m, 40m, 8m),
        new("Soap", UnitOfMeasure.Piece, 30m, 45m, 14m, 5m),
        new("Charcoal", UnitOfMeasure.Kg, 40m, 70m, 3m, 5m),
        new("Eggs (tray)", UnitOfMeasure.Piece, 280m, 360m, 7m, 3m),
        new("Wheat flour", UnitOfMeasure.Kg, 80m, 110m, 4m, 5m),
        new("Drinking water", UnitOfMeasure.Litre, 20m, 40m, 2m, 6m)
    };

    private static readonly CustomerSeed[] CustomerSeeds =
    {
        new("Amina Wanjiku", "0712 000 001"),
        new("Joseph Otieno", "0722 000 002"),
        new("Peter Kamau", "0733 000 003"),
        new("Grace Achieng", "0700 000 004"),
        new("Mary Njeri", "0711 000 005"),
        new("David Kiprop", "0720 000 006"),
        new("Fatuma Ali", "0740 000 007"),
        new("Samuel Mwangi", "0715 000 008"),
        new("Helen Chebet", "0707 000 009"),
        new("Brian Omondi", "0790 000 010"),
        new("Lucy Wairimu", "0755 000 011")
    };

    private sealed record GoodSeed(
        string Name,
        UnitOfMeasure Unit,
        decimal Cost,
        decimal Price,
        decimal Opening,
        decimal Low);

    private sealed record CustomerSeed(string Name, string Phone);
}
