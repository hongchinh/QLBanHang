using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Stock vouchers: sales (XBH), purchases (NMH), returns (NTL, XTL) and damaged goods (XKH).
internal sealed partial class SampleDocumentSimulator
{
    private const decimal VatRate = 10m;

    /// Working days in a year (Monday to Saturday), to turn the monthly purchasing pace into a daily one.
    private const decimal WorkingDaysPerYear = 313m;

    /// Planned product picks per week over all sales: sales per week × average goods lines per sale.
    private static readonly decimal PicksPerWeek =
        (SalesPerMonthMin + SalesPerMonthMax) / 2m * 12m / 52m * (SaleLinesMin + SaleLinesMax) / 2m;

    private readonly List<(DateOnly Date, SampleStockVoucherStep Step)> _sales = new();
    private readonly List<(DateOnly Date, SampleStockVoucherStep Step)> _purchases = new();
    private decimal _purchaseTokens = 1m;

    private void Sale()
    {
        // Shortly before the debt offset, the dual-role partner gets a credit sale if it has no open one.
        var customer = OffsetNeedsSale() ? _profiles[DualRolePartner] : PickCustomer();
        var warehouse = _random.Chance(K1SalePercent) ? K1 : K2;
        var available = InStock(warehouse);
        if (available.Count == 0)
        {
            warehouse = warehouse == K1 ? K2 : K1;
            available = InStock(warehouse);
            if (available.Count == 0)
                return;
        }

        // A line sells about a week of the product's demand in this warehouse divided by how often the product is picked
        // there (picks per week × warehouse share / products in stock), so planned and simulated sales agree.
        var discount = DiscountRate(customer.Group);
        var tier = customer.Tier switch
        {
            SalesTier.Large => LargeQuantityPercent,
            SalesTier.Small => SmallQuantityPercent,
            _ => MediumQuantityPercent,
        };
        var lines = new List<SampleStockLine>();
        foreach (var product in PickDistinct(available, Math.Min(_random.Between(SaleLinesMin, SaleLinesMax), available.Count)))
        {
            var planned = product.WeeklySales * DemandPercent / 100m * available.Count / PicksPerWeek * tier / 100m;
            var spread = 1m + ((decimal)_random.NextDouble() * 2m - 1m) * SaleQuantitySpreadPercent / 100m;
            var quantity = Math.Clamp(Math.Round(planned * spread, MidpointRounding.AwayFromZero), 1m,
                _ledger.Stock(warehouse, product.ProductCode));
            lines.Add(new SampleStockLine(product.ProductCode, quantity, _products[product.ProductCode].DefaultPrice, discount,
                VatRate));
        }
        if (_random.Chance(TransportLinePercent))
        {
            var transport = _products[SampleCatalog.TransportProductCode];
            lines.Add(new SampleStockLine(transport.Code, 1m, transport.DefaultPrice, discount, VatRate));
        }

        var total = _ledger.Total(StockDirection.Out, lines);
        var (method, fund) = SalePayment(customer, total);
        var step = new SampleStockVoucherStep(_ledger.NextKey("XBH"), default, OwnerOf(customer.Code), StockDirection.Out, "XBH",
            warehouse, customer.Code, method, fund, null, lines);
        Write(step);
        foreach (var line in lines)
            _ledger.AddStock(warehouse, line.ProductCode, -line.Quantity);
        _sales.Add((_date, step));

        if (fund != null)
        {
            _ledger.MoveFund(fund, total);
            return;
        }
        var due = _date.AddDays(_partners[customer.Code].CreditDays ?? 0);
        var payDate = CollectionDate(customer, due, opening: false)!.Value;
        _ledger.Items.Add(new SampleOpenItem(step.Key, customer.Code, DebtSide.Receivable, "XBH", false, total,
            payDate > _date ? payDate : _date.AddDays(1)));
    }

    private SampleCustomerProfile PickCustomer()
    {
        var roll = _random.Between(1, 100);
        var tier = roll <= LargeTierPercent ? SalesTier.Large
            : roll <= LargeTierPercent + SmallTierPercent ? SalesTier.Small
            : SalesTier.Medium;
        return _random.Pick(SampleCatalog.CustomerProfiles.Where(p => p.Tier == tier).ToList());
    }

    /// Retail pays cash at once; others pay at once by chance, else buy on credit unless that breaks 80% of their limit.
    /// The dual-role partner's planned sale before the offset is on credit. On the deposit day, while the till is short,
    /// sales are paid in cash, so the week ends with a deposit.
    private (string? Method, string? Fund) SalePayment(SampleCustomerProfile customer, decimal total)
    {
        if (customer.Group == CustomerGroup.Retail)
            return ("TM", CashFund);

        var paidAtOnce = _random.Chance(PaidSalePercent);
        if (OffsetNeedsSale())
            return (null, null);
        if (DepositIsShort())
            return ("TM", CashFund);
        if (paidAtOnce)
            return _random.Chance(PaidSaleBankPercent) ? ("CK", _random.Pick(BankFunds)) : ("TM", CashFund);

        if (customer.CreditLimit is { } limit && _ledger.OpenReceivable(customer.Code) + total > limit * CreditLimitUsePercent / 100m)
            return ("CK", _random.Pick(BankFunds));
        return (null, null);
    }

    private static decimal DiscountRate(CustomerGroup group) => group switch
    {
        CustomerGroup.Agent => 5m,
        CustomerGroup.Project => 3m,
        CustomerGroup.Company => 2m,
        _ => 0m,
    };

    /// Demand of a product in a warehouse, per week.
    private static decimal Demand(string warehouse, SampleProductPlan product) =>
        product.WeeklySales * DemandPercent / 100m * (warehouse == K1 ? K1SalePercent : 100 - K1SalePercent) / 100m;

    private decimal Cover(string warehouse, SampleProductPlan product) =>
        _ledger.Stock(warehouse, product.ProductCode) / Demand(warehouse, product);

    private List<SampleProductPlan> InStock(string warehouse) =>
        SampleCatalog.ProductPlans.Where(p => _ledger.Stock(warehouse, p.ProductCode) >= 1m).ToList();

    private List<T> PickDistinct<T>(IReadOnlyList<T> items, int count)
    {
        var left = items.ToList();
        var picked = new List<T>();
        for (var i = 0; i < count; i++)
        {
            var index = _random.Between(0, left.Count - 1);
            picked.Add(left[index]);
            left.RemoveAt(index);
        }
        return picked;
    }

    /// Places the due reorders, most urgent first (lowest cover; warehouse then product order on a tie), at the
    /// purchasing pace. A product is due when it is stocked in the warehouse and fell under its reorder threshold.
    private void Purchases()
    {
        _purchaseTokens = Math.Min(PurchaseBurst, _purchaseTokens + PurchasesPerMonth * 12m / WorkingDaysPerYear);
        for (var done = 0; done < PurchasesPerDayMax && _purchaseTokens >= 1m; done++)
        {
            (string Warehouse, SampleProductPlan Product)? urgent = null;
            var lowest = ReorderWeeks;
            foreach (var warehouse in WarehouseKeys)
            {
                foreach (var product in SampleCatalog.ProductPlans)
                {
                    if (!_ledger.IsStocked(warehouse, product.ProductCode) || Cover(warehouse, product) >= lowest)
                        continue;
                    lowest = Cover(warehouse, product);
                    urgent = (warehouse, product);
                }
            }
            if (urgent is not { } due)
                return;
            Purchase(due.Warehouse, due.Product);
            _purchaseTokens -= 1m;
        }
    }

    /// Restocks the product group of `trigger` (its primary supplier's products) in the warehouse, bought from the primary
    /// or the alternate supplier.
    private void Purchase(string warehouse, SampleProductPlan trigger)
    {
        // Until the debt offset, a purchase of the dual-role partner's products goes to it on credit while it has no open one.
        var forDualRole = trigger.AltSupplier == DualRolePartner && !_offsetDone && OpenItem(DualRolePartner, "NMH") == null;
        var useAlt = _random.Chance(AltSupplierPercent);
        var supplier = forDualRole ? DualRolePartner : useAlt && trigger.AltSupplier != null ? trigger.AltSupplier : trigger.PrimarySupplier;
        var (restockMin, restockMax) = warehouse == K1 ? (RestockWeeksK1Min, RestockWeeksK1Max) : (RestockWeeksK2Min, RestockWeeksK2Max);

        var candidates = SampleCatalog.ProductPlans
            .Where(p => p.PrimarySupplier == trigger.PrimarySupplier)
            .ToList();
        var stocked = candidates
            .Where(p => _ledger.IsStocked(warehouse, p.ProductCode))
            .OrderBy(p => Cover(warehouse, p))
            .ToList();
        var chosen = stocked.Where(p => Cover(warehouse, p) < restockMin).Take(PurchaseLinesMax).ToList();
        foreach (var extra in stocked.Concat(candidates.Where(p => !_ledger.IsStocked(warehouse, p.ProductCode))))
        {
            if (chosen.Count >= PurchaseLinesMin)
                break;
            if (!chosen.Contains(extra))
                chosen.Add(extra);
        }

        var lines = new List<SampleStockLine>();
        foreach (var product in chosen)
        {
            // Up to the restock level; a product added only to reach the minimum line count gets at least a week of demand.
            var weeks = restockMin + (decimal)_random.NextDouble() * (restockMax - restockMin);
            var demand = Demand(warehouse, product);
            var quantity = RoundUpNicely(Math.Max(demand * weeks - _ledger.Stock(warehouse, product.ProductCode), demand));
            var factor = 1m + ((decimal)_random.NextDouble() * 2m - 1m) * PurchasePriceSpreadPercent / 100m;
            var price = Math.Round(_products[product.ProductCode].CostPrice * factor / 100m, MidpointRounding.AwayFromZero) * 100m;
            lines.Add(new SampleStockLine(product.ProductCode, quantity, price, 0m, VatRate));
        }

        var total = _ledger.Total(StockDirection.In, lines);
        var paidAtOnce = _random.Chance(PaidPurchasePercent);
        var fund = paidAtOnce && !forDualRole ? PayingBank(total) : null;
        var step = new SampleStockVoucherStep(_ledger.NextKey("NMH"), default, null, StockDirection.In, "NMH", warehouse, supplier,
            fund != null ? "CK" : null, fund, null, lines);
        Write(step);
        foreach (var line in lines)
            _ledger.AddStock(warehouse, line.ProductCode, line.Quantity);
        _purchases.Add((_date, step));

        if (fund != null)
        {
            _ledger.MoveFund(fund, -total);
            return;
        }
        var due = _date.AddDays(_partners[supplier].CreditDays ?? 0);
        var payDate = due.AddDays(-_random.Between(SupplierDaysBeforeDueMin, SupplierDaysBeforeDueMax));
        _ledger.Items.Add(new SampleOpenItem(step.Key, supplier, DebtSide.Payable, "NMH", false, total,
            payDate > _date ? payDate : _date.AddDays(1)));
    }

    /// At least 1; then up to a multiple of 5 above 20 and of 10 above 200.
    private static decimal RoundUpNicely(decimal quantity)
    {
        var step = quantity > 200m ? 10m : quantity > 20m ? 5m : 1m;
        return Math.Max(1m, Math.Ceiling(quantity / step) * step);
    }

    /// Part of a sale of the last days, same customer and warehouse, on credit.
    private bool SaleReturn()
    {
        var recent = _sales
            .Where(s => s.Date < _date && s.Date >= _date.AddDays(-SaleReturnLookbackDays))
            .Select(s => s.Step)
            .ToList();
        if (recent.Count == 0)
            return false;

        var sale = _random.Pick(recent);
        var goods = sale.Lines.Where(l => _products[l.ProductCode].TrackInventory).ToList();
        var lines = PickDistinct(goods, Math.Min(_random.Between(1, ReturnLinesMax), goods.Count))
            .Select(l => l with { Quantity = Math.Clamp(ReturnedPart(l.Quantity), 1m, l.Quantity) })
            .ToList();
        var step = new SampleStockVoucherStep(_ledger.NextKey("NTL"), default, OwnerOf(sale.PartnerCode!), StockDirection.In, "NTL",
            sale.WarehouseKey, sale.PartnerCode, null, null, "Khách trả lại hàng", lines);
        Write(step);
        foreach (var line in lines)
            _ledger.AddStock(sale.WarehouseKey, line.ProductCode, line.Quantity);
        return true;
    }

    /// Part of the latest purchase that still has stock, same supplier, on credit.
    private bool PurchaseReturn()
    {
        for (var i = _purchases.Count - 1; i >= 0; i--)
        {
            var purchase = _purchases[i].Step;
            var warehouse = purchase.WarehouseKey;
            var goods = purchase.Lines.Where(l => _ledger.Stock(warehouse, l.ProductCode) >= 1m).ToList();
            if (goods.Count == 0)
                continue;

            var lines = PickDistinct(goods, Math.Min(_random.Between(1, ReturnLinesMax), goods.Count))
                .Select(l => l with { Quantity = Math.Clamp(ReturnedPart(l.Quantity), 1m, _ledger.Stock(warehouse, l.ProductCode)) })
                .ToList();
            var step = new SampleStockVoucherStep(_ledger.NextKey("XTL"), default, null, StockDirection.Out, "XTL", warehouse,
                purchase.PartnerCode, null, null, "Trả lại hàng lỗi cho nhà cung cấp", lines);
            Write(step);
            foreach (var line in lines)
                _ledger.AddStock(warehouse, line.ProductCode, -line.Quantity);
            return true;
        }
        return false;
    }

    private decimal ReturnedPart(decimal quantity) =>
        Math.Round(quantity * _random.Between(ReturnPercentMin, ReturnPercentMax) / 100m, MidpointRounding.AwayFromZero);

    /// One or two small lines written off, at no price.
    private bool Damaged()
    {
        var warehouse = _random.Chance(K1SalePercent) ? K1 : K2;
        var available = InStock(warehouse);
        if (available.Count == 0)
            return false;

        var lines = PickDistinct(available, Math.Min(_random.Between(1, DamagedLinesMax), available.Count))
            .Select(p => new SampleStockLine(p.ProductCode,
                Math.Clamp(Math.Round(p.WeeklySales * _random.Between(1, DamagedPercentMax) / 100m, MidpointRounding.AwayFromZero),
                    1m, _ledger.Stock(warehouse, p.ProductCode)),
                0m, 0m, 0m))
            .ToList();
        var step = new SampleStockVoucherStep(_ledger.NextKey("XKH"), default, null, StockDirection.Out, "XKH", warehouse, null,
            null, null, "Hàng hỏng", lines);
        Write(step);
        foreach (var line in lines)
            _ledger.AddStock(warehouse, line.ProductCode, -line.Quantity);
        return true;
    }
}
