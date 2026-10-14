using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Stock vouchers other than sales: purchases (NMH) with the end-low rule, returns (NTL, XTL) and damaged goods (XKH).
internal sealed partial class SampleDocumentSimulator
{
    private const decimal VatRate = 10m;

    /// Working days in a year (Monday to Saturday), to turn the monthly purchasing pace into a daily one.
    private const decimal WorkingDaysPerYear = 313m;

    private readonly List<(DateOnly Date, SampleStockVoucherStep Step)> _purchases = new();
    private decimal _purchaseTokens = 1m;
    private HashSet<string>? _endLow;

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
        if (_endLow is null && _date >= _plan.LastDocumentDate.AddDays(-EndLowDays))
            _endLow = PickEndLowProducts();
        _purchaseTokens = Math.Min(PurchaseBurst, _purchaseTokens + PurchasesPerMonth * 12m / WorkingDaysPerYear);
        for (var done = 0; done < PurchasesPerDayMax && _purchaseTokens >= 1m; done++)
        {
            (string Warehouse, SampleProductPlan Product)? urgent = null;
            var lowest = ReorderWeeks;
            foreach (var warehouse in WarehouseKeys)
            {
                foreach (var product in SampleCatalog.ProductPlans)
                {
                    if (!_ledger.IsStocked(warehouse, product.ProductCode) || IsEndLow(product)
                        || Cover(warehouse, product) >= lowest)
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

    private bool IsEndLow(SampleProductPlan product) => _endLow?.Contains(product.ProductCode) == true;

    /// The EndLowProducts products expected to end lowest after the last EndLowDays days of sales, taking from a product
    /// group only as many as leave it PurchaseLinesMin products to buy. They are no longer bought, so they end the window
    /// below a week of sales.
    private HashSet<string> PickEndLowProducts()
    {
        var weeksLeft = (_plan.LastDocumentDate.DayNumber - _date.DayNumber + 1) / 7m;
        var plans = SampleCatalog.ProductPlans;
        decimal ProjectedWeeks(SampleProductPlan p) =>
            WarehouseKeys.Sum(w => _ledger.Stock(w, p.ProductCode)
                - (_ledger.IsStocked(w, p.ProductCode) ? Demand(w, p) * weeksLeft : 0m)) / p.WeeklySales;
        int Spare(string group) => plans.Count(p => p.PrimarySupplier == group) - PurchaseLinesMin;

        var picked = new List<SampleProductPlan>();
        foreach (var product in plans.OrderBy(ProjectedWeeks))
        {
            if (picked.Count == EndLowProducts)
                break;
            if (picked.Count(p => p.PrimarySupplier == product.PrimarySupplier) < Spare(product.PrimarySupplier))
                picked.Add(product);
        }
        return picked.Select(p => p.ProductCode).ToHashSet();
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
            .Where(p => p.PrimarySupplier == trigger.PrimarySupplier && !IsEndLow(p))
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
        _ledger.Items.Add(new SampleOpenItem(step.Key, supplier, DebtSide.Payable, "NMH", false, total, due,
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
