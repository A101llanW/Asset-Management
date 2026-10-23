using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SmallShop.Core;

public sealed class ShopService
{
    private readonly ShopDbContext _db;

    public ShopService(ShopDbContext db)
    {
        _db = db;
    }

    public async Task<Good> AddGoodAsync(
        string name,
        UnitOfMeasure unit,
        decimal cost,
        decimal sellingPrice,
        decimal openingQuantity,
        decimal lowStockThreshold,
        DateTime? when = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name, "Give the good a name.");
        if (cost < 0 || sellingPrice < 0 || openingQuantity < 0 || lowStockThreshold < 0)
        {
            throw new ShopRuleException("Cost, selling price, opening stock, and the low-stock level cannot be negative.");
        }

        Good? created = null;
        await InTransaction(async ct =>
        {
            var opening = ShopMath.Qty(openingQuantity);
            var good = new Good
            {
                Name = trimmed,
                Unit = unit,
                Cost = ShopMath.Money(cost),
                SellingPrice = ShopMath.Money(sellingPrice),
                QuantityOnHand = opening,
                LowStockThreshold = ShopMath.Qty(lowStockThreshold)
            };
            _db.Goods.Add(good);
            _db.StockMovements.Add(new StockMovement
            {
                Good = good,
                Kind = StockMovementKind.Opening,
                QuantityDelta = opening,
                QuantityAfter = opening,
                Reason = "Opening stock",
                OccurredAt = At(when)
            });
            created = good;
            await Task.CompletedTask;
        }, cancellationToken);

        return created ?? throw new ShopRuleException("The good was not saved.");
    }

    public async Task UpdateGoodAsync(
        int goodId,
        string name,
        UnitOfMeasure unit,
        decimal cost,
        decimal sellingPrice,
        decimal lowStockThreshold,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name, "Give the good a name.");
        if (cost < 0 || sellingPrice < 0 || lowStockThreshold < 0)
        {
            throw new ShopRuleException("Cost, selling price, and the low-stock level cannot be negative.");
        }

        await InTransaction(async ct =>
        {
            var good = await FindGoodAsync(goodId, ct);
            good.Name = trimmed;
            good.Unit = unit;
            good.Cost = ShopMath.Money(cost);
            good.SellingPrice = ShopMath.Money(sellingPrice);
            good.LowStockThreshold = ShopMath.Qty(lowStockThreshold);
        }, cancellationToken);
    }

    public async Task SetOpeningStockAsync(
        int goodId,
        decimal quantity,
        DateTime? when = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 0)
        {
            throw new ShopRuleException("Opening stock cannot be negative.");
        }

        await InTransaction(async ct =>
        {
            var good = await FindGoodAsync(goodId, ct);
            var count = ShopMath.Qty(quantity);
            var delta = ShopMath.Qty(count - good.QuantityOnHand);
            good.QuantityOnHand = count;
            AddMovement(good, StockMovementKind.Opening, delta, null, "Opening stock", At(when), null, null, null);
        }, cancellationToken);
    }

    public async Task AdjustStockAsync(
        int goodId,
        StockAdjustmentKind kind,
        decimal quantity,
        string reason,
        DateTime? when = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedReason = RequireReason(reason, "A stock adjustment needs a reason.");
        await InTransaction(async ct =>
        {
            var good = await FindGoodAsync(goodId, ct);
            var amount = ShopMath.Qty(quantity);
            decimal delta;
            if (kind == StockAdjustmentKind.Count)
            {
                if (amount < 0)
                {
                    throw new ShopRuleException("A count cannot be negative.");
                }

                delta = ShopMath.Qty(amount - good.QuantityOnHand);
                good.QuantityOnHand = amount;
            }
            else
            {
                if (amount <= 0)
                {
                    throw new ShopRuleException("Quantity must be greater than zero.");
                }

                if (amount > good.QuantityOnHand)
                {
                    throw new ShopRuleException("That is more than the quantity on hand.");
                }

                delta = -amount;
                good.QuantityOnHand = ShopMath.Qty(good.QuantityOnHand - amount);
            }

            var movementKind = kind switch
            {
                StockAdjustmentKind.Count => StockMovementKind.Count,
                StockAdjustmentKind.Damage => StockMovementKind.Damage,
                StockAdjustmentKind.Theft => StockMovementKind.Theft,
                _ => throw new ShopRuleException("Choose a count, damage, or theft.")
            };
            AddMovement(good, movementKind, delta, null, trimmedReason, At(when), null, null, null);
        }, cancellationToken);
    }

    public async Task<Customer> AddCustomerAsync(string name, string phone, CancellationToken cancellationToken = default)
    {
        var trimmedName = RequireName(name, "Give the customer a name.");
        var trimmedPhone = RequirePhone(phone);
        Customer? created = null;
        await InTransaction(async ct =>
        {
            created = new Customer { Name = trimmedName, Phone = trimmedPhone };
            _db.Customers.Add(created);
            await Task.CompletedTask;
        }, cancellationToken);
        return created ?? throw new ShopRuleException("The customer was not saved.");
    }

    public async Task<Supplier> AddSupplierAsync(string name, string phone, CancellationToken cancellationToken = default)
    {
        var trimmedName = RequireName(name, "Give the supplier a name.");
        var trimmedPhone = RequirePhone(phone);
        Supplier? created = null;
        await InTransaction(async ct =>
        {
            created = new Supplier { Name = trimmedName, Phone = trimmedPhone };
            _db.Suppliers.Add(created);
            await Task.CompletedTask;
        }, cancellationToken);
        return created ?? throw new ShopRuleException("The supplier was not saved.");
    }

    public async Task LinkGoodAsync(
        int supplierId,
        int goodId,
        decimal supplyPrice,
        CancellationToken cancellationToken = default)
    {
        if (supplyPrice < 0)
        {
            throw new ShopRuleException("Supply price cannot be negative.");
        }

        await InTransaction(async ct =>
        {
            _ = await FindSupplierAsync(supplierId, ct);
            _ = await FindGoodAsync(goodId, ct);
            var exists = await _db.SupplierGoods.AnyAsync(
                x => x.SupplierId == supplierId && x.GoodId == goodId,
                ct);
            if (exists)
            {
                throw new ShopRuleException("This supplier already supplies that good.");
            }

            _db.SupplierGoods.Add(new SupplierGood
            {
                SupplierId = supplierId,
                GoodId = goodId,
                SupplyPrice = ShopMath.Money(supplyPrice)
            });
        }, cancellationToken);
    }

    public async Task SetSupplyPriceAsync(
        int supplierId,
        int goodId,
        decimal supplyPrice,
        CancellationToken cancellationToken = default)
    {
        if (supplyPrice < 0)
        {
            throw new ShopRuleException("Supply price cannot be negative.");
        }

        await InTransaction(async ct =>
        {
            var link = await _db.SupplierGoods.FirstOrDefaultAsync(
                x => x.SupplierId == supplierId && x.GoodId == goodId,
                ct);
            if (link == null)
            {
                throw new ShopRuleException("This supplier does not supply that good.");
            }

            link.SupplyPrice = ShopMath.Money(supplyPrice);
        }, cancellationToken);
    }

    public async Task<GoodsOutRecord> RecordGoodsOutAsync(
        GoodsOutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines == null || request.Lines.Count == 0)
        {
            throw new ShopRuleException("Add at least one good.");
        }

        GoodsOutRecord? created = null;
        await InTransaction(async ct =>
        {
            var priced = new List<(Good Good, decimal Quantity, decimal UnitPrice, decimal LineTotal)>();
            var seen = new HashSet<int>();
            foreach (var line in request.Lines)
            {
                if (line.GoodId == 0 && line.Quantity == 0)
                {
                    continue;
                }

                if (line.GoodId == 0)
                {
                    throw new ShopRuleException("Choose a good.");
                }

                if (line.Quantity <= 0)
                {
                    throw new ShopRuleException("Quantity must be greater than zero.");
                }

                if (!seen.Add(line.GoodId))
                {
                    throw new ShopRuleException("List each good once on a goods-out.");
                }

                var good = await FindGoodAsync(line.GoodId, ct);
                var quantity = ShopMath.Qty(line.Quantity);
                if (quantity <= 0)
                {
                    throw new ShopRuleException("Quantity must be greater than zero.");
                }

                if (quantity > good.QuantityOnHand)
                {
                    throw new ShopRuleException("Not enough " + good.Name + " in stock.");
                }

                var price = line.UnitPrice.HasValue
                    ? ShopMath.Money(line.UnitPrice.Value)
                    : good.SellingPrice;
                if (price < 0)
                {
                    throw new ShopRuleException("Price cannot be negative.");
                }

                priced.Add((good, quantity, price, ShopMath.Money(quantity * price)));
            }

            if (priced.Count == 0)
            {
                throw new ShopRuleException("Add at least one good.");
            }

            var total = ShopMath.Money(priced.Sum(line => line.LineTotal));
            var paid = ShopMath.Money(request.AmountPaid);
            if (paid < 0)
            {
                throw new ShopRuleException("Amount paid cannot be negative.");
            }

            Customer? customer = null;
            if (request.CustomerId is int customerId && customerId > 0)
            {
                customer = await FindCustomerAsync(customerId, ct);
                if (paid > total)
                {
                    throw new ShopRuleException("Amount paid cannot exceed the goods-out total.");
                }
            }
            else if (paid < total)
            {
                throw new ShopRuleException("A walk-in goods-out cannot create debt.");
            }

            var charged = customer == null ? 0m : ShopMath.Money(total - paid);
            var when = At(request.When);
            var summary = string.Join(", ", priced.Select(line =>
                line.Good.Name + " " + QtyText(line.Quantity)));
            var note = "Goods out: " + summary + ". Total " + MoneyText(total) + ", paid " + MoneyText(paid) + ".";

            var record = new GoodsOutRecord
            {
                CustomerId = customer?.Id,
                OccurredAt = when,
                Total = total,
                AmountPaid = paid,
                AmountCharged = charged,
                TenderMethod = paid > 0 ? request.TenderMethod ?? PaymentMethod.Cash : null
            };
            _db.GoodsOutRecords.Add(record);

            foreach (var line in priced)
            {
                record.Lines.Add(new GoodsOutLine
                {
                    GoodId = line.Good.Id,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal
                });
                line.Good.QuantityOnHand = ShopMath.Qty(line.Good.QuantityOnHand - line.Quantity);
                AddMovement(
                    line.Good,
                    StockMovementKind.GoodsOut,
                    -line.Quantity,
                    line.UnitPrice,
                    note,
                    when,
                    customer?.Id,
                    null,
                    record);
            }

            if (customer != null && charged > 0)
            {
                IncreaseDebt(customer, charged, when);
                _db.LedgerEntries.Add(new LedgerEntry
                {
                    CustomerId = customer.Id,
                    Kind = LedgerEntryKind.Charge,
                    Amount = charged,
                    BalanceAfter = customer.Balance,
                    Note = note,
                    OccurredAt = when,
                    GoodsOutRecord = record
                });
            }

            created = record;
        }, cancellationToken);

        return created ?? throw new ShopRuleException("The goods-out was not saved.");
    }

    public async Task RecordPaymentAsync(
        int customerId,
        decimal amount,
        PaymentMethod method,
        DateTime? when = null,
        CancellationToken cancellationToken = default)
    {
        var payment = ShopMath.Money(amount);
        if (payment <= 0)
        {
            throw new ShopRuleException("Enter a payment greater than zero.");
        }

        await InTransaction(async ct =>
        {
            var customer = await FindCustomerAsync(customerId, ct);
            if (payment > customer.Balance)
            {
                throw new ShopRuleException("A payment cannot exceed the balance.");
            }

            DecreaseDebt(customer, payment);
            _db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = customer.Id,
                Kind = LedgerEntryKind.Payment,
                Amount = -payment,
                BalanceAfter = customer.Balance,
                PaymentMethod = method,
                Note = ShopLabels.Method(method),
                OccurredAt = At(when)
            });
        }, cancellationToken);
    }

    public async Task RecordReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ShopRuleException("Quantity must be greater than zero.");
        }

        await InTransaction(async ct =>
        {
            var customer = await FindCustomerAsync(request.CustomerId, ct);
            var good = await FindGoodAsync(request.GoodId, ct);
            var quantity = ShopMath.Qty(request.Quantity);
            if (quantity <= 0)
            {
                throw new ShopRuleException("Quantity must be greater than zero.");
            }

            var taken = await _db.GoodsOutLines
                .Where(line => line.GoodsOutRecord.CustomerId == customer.Id && line.GoodId == good.Id)
                .SumAsync(line => (decimal?)line.Quantity, ct) ?? 0m;
            var alreadyReturned = await _db.StockMovements
                .Where(movement =>
                    movement.CustomerId == customer.Id &&
                    movement.GoodId == good.Id &&
                    movement.Kind == StockMovementKind.Return)
                .SumAsync(movement => (decimal?)movement.QuantityDelta, ct) ?? 0m;
            if (ShopMath.Qty(taken - alreadyReturned) < quantity)
            {
                throw new ShopRuleException("A return has to be goods this customer took.");
            }

            decimal price;
            if (request.UnitPrice.HasValue)
            {
                price = ShopMath.Money(request.UnitPrice.Value);
            }
            else
            {
                price = await _db.GoodsOutLines
                    .Where(line => line.GoodsOutRecord.CustomerId == customer.Id && line.GoodId == good.Id)
                    .OrderByDescending(line => line.GoodsOutRecord.OccurredAt)
                    .ThenByDescending(line => line.Id)
                    .Select(line => line.UnitPrice)
                    .FirstAsync(ct);
            }

            if (price < 0)
            {
                throw new ShopRuleException("Price cannot be negative.");
            }

            var credit = ShopMath.Money(quantity * price);
            if (credit > customer.Balance)
            {
                throw new ShopRuleException("A return cannot exceed the balance.");
            }

            var when = At(request.When);
            good.QuantityOnHand = ShopMath.Qty(good.QuantityOnHand + quantity);
            var note = "Return: " + good.Name + " " + QtyText(quantity) + ".";
            AddMovement(good, StockMovementKind.Return, quantity, price, note, when, customer.Id, null, null);
            if (credit > 0)
            {
                DecreaseDebt(customer, credit);
                _db.LedgerEntries.Add(new LedgerEntry
                {
                    CustomerId = customer.Id,
                    Kind = LedgerEntryKind.Return,
                    Amount = -credit,
                    BalanceAfter = customer.Balance,
                    Note = note,
                    OccurredAt = when
                });
            }
        }, cancellationToken);
    }

    public async Task WriteOffAsync(
        int customerId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireReason(reason, "A write-off needs a reason.");
        var writeOff = ShopMath.Money(amount);
        if (writeOff <= 0)
        {
            throw new ShopRuleException("Write-off amount must be greater than zero.");
        }

        await InTransaction(async ct =>
        {
            var customer = await FindCustomerAsync(customerId, ct);
            if (writeOff > customer.Balance)
            {
                throw new ShopRuleException("A write-off cannot exceed the balance.");
            }

            DecreaseDebt(customer, writeOff);
            _db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = customer.Id,
                Kind = LedgerEntryKind.WriteOff,
                Amount = -writeOff,
                BalanceAfter = customer.Balance,
                Reason = trimmed,
                Note = trimmed,
                OccurredAt = At(null)
            });
        }, cancellationToken);
    }

    public async Task AdjustBalanceAsync(
        int customerId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireReason(reason, "A balance adjustment needs a reason.");
        var adjustment = ShopMath.Money(amount);
        if (adjustment == 0)
        {
            throw new ShopRuleException("Enter an adjustment amount.");
        }

        await InTransaction(async ct =>
        {
            var customer = await FindCustomerAsync(customerId, ct);
            var next = ShopMath.Money(customer.Balance + adjustment);
            if (next < 0)
            {
                throw new ShopRuleException("An adjustment cannot take the balance below zero.");
            }

            var when = At(null);
            if (adjustment > 0)
            {
                IncreaseDebt(customer, adjustment, when);
            }
            else
            {
                DecreaseDebt(customer, -adjustment);
            }

            _db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = customer.Id,
                Kind = LedgerEntryKind.Adjustment,
                Amount = adjustment,
                BalanceAfter = customer.Balance,
                Reason = trimmed,
                Note = trimmed,
                OccurredAt = when
            });
        }, cancellationToken);
    }

    public async Task<GoodsReceipt> ReceiveAsync(ReceiveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines == null || request.Lines.Count == 0)
        {
            throw new ShopRuleException("Enter a quantity for at least one good.");
        }

        GoodsReceipt? created = null;
        await InTransaction(async ct =>
        {
            var supplier = await _db.Suppliers
                .Include(item => item.Goods)
                .FirstOrDefaultAsync(item => item.Id == request.SupplierId, ct);
            if (supplier == null)
            {
                throw new ShopRuleException("That supplier was not found.");
            }

            var catalog = supplier.Goods.Select(item => item.GoodId).ToHashSet();
            var accepted = new List<(int GoodId, decimal Quantity, decimal UnitCost, bool UpdateCost)>();
            var seen = new HashSet<int>();
            foreach (var line in request.Lines)
            {
                if (line.Quantity == 0)
                {
                    continue;
                }

                if (line.Quantity < 0)
                {
                    throw new ShopRuleException("Quantity must be greater than zero.");
                }

                if (line.UnitCost < 0)
                {
                    throw new ShopRuleException("Cost cannot be negative.");
                }

                if (!catalog.Contains(line.GoodId))
                {
                    throw new ShopRuleException("Receive only goods this supplier supplies.");
                }

                if (!seen.Add(line.GoodId))
                {
                    throw new ShopRuleException("List each good once on a delivery.");
                }

                var quantity = ShopMath.Qty(line.Quantity);
                if (quantity <= 0)
                {
                    throw new ShopRuleException("Quantity must be greater than zero.");
                }

                accepted.Add((line.GoodId, quantity, ShopMath.Money(line.UnitCost), line.UpdateCost));
            }

            if (accepted.Count == 0)
            {
                throw new ShopRuleException("Enter a quantity for at least one good.");
            }

            var ids = accepted.Select(line => line.GoodId).ToList();
            var goods = await _db.Goods.Where(good => ids.Contains(good.Id)).ToDictionaryAsync(good => good.Id, ct);
            var when = At(request.When);
            var receipt = new GoodsReceipt
            {
                SupplierId = supplier.Id,
                OccurredAt = when
            };
            _db.GoodsReceipts.Add(receipt);

            foreach (var line in accepted)
            {
                var good = goods[line.GoodId];
                if (line.UpdateCost)
                {
                    good.Cost = line.UnitCost;
                }

                good.QuantityOnHand = ShopMath.Qty(good.QuantityOnHand + line.Quantity);
                receipt.Lines.Add(new GoodsReceiptLine
                {
                    GoodId = good.Id,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    UpdatedCost = line.UpdateCost
                });
                AddMovement(
                    good,
                    StockMovementKind.Delivery,
                    line.Quantity,
                    line.UnitCost,
                    "Received from " + supplier.Name,
                    when,
                    null,
                    supplier.Id,
                    null);
            }

            created = receipt;
        }, cancellationToken);

        return created ?? throw new ShopRuleException("The delivery was not saved.");
    }

    private async Task<Good> FindGoodAsync(int goodId, CancellationToken cancellationToken)
    {
        var good = await _db.Goods.FirstOrDefaultAsync(item => item.Id == goodId, cancellationToken);
        if (good == null)
        {
            throw new ShopRuleException("That good was not found.");
        }

        return good;
    }

    private async Task<Customer> FindCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        if (customer == null)
        {
            throw new ShopRuleException("That customer was not found.");
        }

        return customer;
    }

    private async Task<Supplier> FindSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(item => item.Id == supplierId, cancellationToken);
        if (supplier == null)
        {
            throw new ShopRuleException("That supplier was not found.");
        }

        return supplier;
    }

    private void AddMovement(
        Good good,
        StockMovementKind kind,
        decimal delta,
        decimal? unitPrice,
        string? reason,
        DateTime when,
        int? customerId,
        int? supplierId,
        GoodsOutRecord? goodsOut)
    {
        _db.StockMovements.Add(new StockMovement
        {
            GoodId = good.Id,
            Good = good,
            Kind = kind,
            QuantityDelta = delta,
            QuantityAfter = good.QuantityOnHand,
            UnitPrice = unitPrice,
            Reason = reason,
            OccurredAt = when,
            CustomerId = customerId,
            SupplierId = supplierId,
            GoodsOutRecord = goodsOut
        });
    }

    private static void IncreaseDebt(Customer customer, decimal amount, DateTime when)
    {
        if (customer.Balance <= 0)
        {
            customer.UnpaidSince = when;
        }

        customer.Balance = ShopMath.Money(customer.Balance + amount);
    }

    private static void DecreaseDebt(Customer customer, decimal amount)
    {
        customer.Balance = ShopMath.Money(customer.Balance - amount);
        if (customer.Balance <= 0)
        {
            customer.Balance = 0;
            customer.UnpaidSince = null;
        }
    }

    private static DateTime At(DateTime? when) => when ?? DateTime.UtcNow;

    private static string MoneyText(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string QtyText(decimal value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string RequireName(string? name, string message)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new ShopRuleException(message);
        }

        if (trimmed.Length > 120)
        {
            throw new ShopRuleException("Use a shorter name (120 characters or fewer).");
        }

        return trimmed;
    }

    private static string RequirePhone(string? phone)
    {
        var trimmed = (phone ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new ShopRuleException("A phone number is required.");
        }

        if (trimmed.Length > 40)
        {
            throw new ShopRuleException("Use a shorter phone number.");
        }

        return trimmed;
    }

    private static string RequireReason(string? reason, string message)
    {
        var trimmed = (reason ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new ShopRuleException(message);
        }

        if (trimmed.Length > 400)
        {
            throw new ShopRuleException("Use a shorter reason (400 characters or fewer).");
        }

        return trimmed;
    }

    private async Task InTransaction(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await work(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            throw;
        }
    }
}
