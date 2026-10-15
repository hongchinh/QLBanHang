using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// An item a later step may collect, pay, refund or offset: an opening debt or prepayment, or a credit sale or purchase.
/// Returns are not tracked: no step targets them, and leaving them out only overstates a credit balance.
internal sealed class SampleOpenItem
{
    public SampleOpenItem(string key, string partnerCode, DebtSide side, string source, bool isPrepayment, decimal open,
        DateOnly? dueDate, DateOnly? payDate)
    {
        Key = key;
        PartnerCode = partnerCode;
        Side = side;
        Source = source;
        IsPrepayment = isPrepayment;
        Open = open;
        DueDate = dueDate;
        PayDate = payDate;
    }

    public string Key { get; }
    public string PartnerCode { get; }
    public DebtSide Side { get; }

    /// "OD" for an opening item, otherwise the reason code of the document.
    public string Source { get; }

    public bool IsPrepayment { get; }

    /// Due date of an invoice; null for a prepayment.
    public DateOnly? DueDate { get; }
    public decimal Open { get; private set; }

    /// First day the partner pays (or is paid); null means never.
    public DateOnly? PayDate { get; private set; }

    /// Takes `amount` off the open amount (never below 0); `payAgain` is when the rest is due to be paid.
    public void Settle(decimal amount, DateOnly? payAgain = null)
    {
        if (amount < 0m || amount > Open)
            throw new InvalidOperationException($"Cannot settle {amount} of {Key}, which has {Open} open.");
        Open -= amount;
        if (payAgain is not null)
            PayDate = payAgain;
    }
}

/// Running state of the document simulation: stock per warehouse and product, estimated fund balances, open items and
/// key counters. Lookups only: nothing here decides an output order by iterating a dictionary.
internal sealed class SampleLedger
{
    private readonly Dictionary<(string Warehouse, string Product), decimal> _stock = new();
    private readonly HashSet<(string Warehouse, string Product)> _stocked = new();
    private readonly Dictionary<string, decimal> _funds = new();
    private readonly Dictionary<string, int> _counters = new();
    private readonly Dictionary<string, SampleProductSpec> _products;
    private readonly bool _netExcludesVat;

    /// `netExcludesVat` is the company-wide `InventorySettings.NetExcludesVat`, so estimated totals match the real ones.
    public SampleLedger(SampleDataPlan plan, bool netExcludesVat)
    {
        _netExcludesVat = netExcludesVat;
        _products = plan.Products.ToDictionary(p => p.Code);
        foreach (var stock in plan.OpeningStocks)
            foreach (var line in stock.Lines)
                AddStock(stock.WarehouseKey, line.ProductCode, line.Quantity);
        foreach (var fund in plan.Funds)
            _funds[fund.Code] = fund.OpeningAmount;
    }

    /// Open items in creation order (opening items first), so allocations go oldest first.
    public List<SampleOpenItem> Items { get; } = new();

    public decimal Stock(string warehouse, string product) => _stock.GetValueOrDefault((warehouse, product));

    /// True once the warehouse has held the product (opening stock or a purchase), even if it is sold out now.
    public bool IsStocked(string warehouse, string product) => _stocked.Contains((warehouse, product));

    public void AddStock(string warehouse, string product, decimal quantity)
    {
        if (!_products[product].TrackInventory)
            return;
        var after = Stock(warehouse, product) + quantity;
        if (after < 0)
            throw new InvalidOperationException($"Sample stock of {product} in {warehouse} would fall to {after}.");
        _stock[(warehouse, product)] = after;
        _stocked.Add((warehouse, product));
    }

    public decimal Fund(string code) => _funds[code];

    public void MoveFund(string code, decimal delta) => _funds[code] += delta;

    public string NextKey(string prefix)
    {
        var number = _counters.GetValueOrDefault(prefix) + 1;
        _counters[prefix] = number;
        return $"{prefix}-{number:0000}";
    }

    /// Estimated voucher total, as the stock voucher service computes it (no freight, no order discount).
    public decimal Total(StockDirection type, IReadOnlyList<SampleStockLine> lines) =>
        StockVoucherCalculator.Compute(new StockHeaderInput(type, 0m, 0m, _netExcludesVat),
            lines.Select(l => new StockLineInput(_products[l.ProductCode].TrackInventory, PricingMode.PerUnit, false,
                null, null, null, null, l.Quantity, l.UnitPrice, l.DiscountRate, null, false, l.VatRate)).ToList()).Totals.Total;

    /// Open receivable invoices of a customer (opening debts and credit sales): an upper bound of its credit balance.
    public decimal OpenReceivable(string customer) =>
        Items.Where(i => i.PartnerCode == customer && i.Side == DebtSide.Receivable && !i.IsPrepayment).Sum(i => i.Open);
}
