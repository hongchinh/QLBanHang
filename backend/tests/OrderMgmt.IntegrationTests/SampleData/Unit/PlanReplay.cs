using OrderMgmt.Application.Inventory.StockVouchers.Services;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

/// Replays a sample plan in step order with estimated amounts, the way the runner will execute it.
/// Pure and independent from the simulator: it reads only the plan.
/// Allocations take min(open, MaxAmount ?? open) per target in target order; paid stock vouchers settle their own item.
public sealed class PlanReplay
{
    public sealed class DebtItem
    {
        public DebtItem(string key, string partnerCode, DebtSide side, bool isIncrease, string source, DateOnly docDate,
            DateOnly? dueDate, decimal amount, int? ownerIndex)
        {
            Key = key;
            PartnerCode = partnerCode;
            Side = side;
            IsIncrease = isIncrease;
            Source = source;
            DocDate = docDate;
            DueDate = dueDate;
            Amount = amount;
            Open = amount;
            OwnerIndex = ownerIndex;
        }

        public string Key { get; }
        public string PartnerCode { get; }
        public DebtSide Side { get; }

        /// Debt (invoice) items increase the balance; prepayments and returns decrease it.
        public bool IsIncrease { get; }

        /// "OD" for an opening item, otherwise the reason code of the stock voucher.
        public string Source { get; }

        public DateOnly DocDate { get; }
        public DateOnly? DueDate { get; }
        public decimal Amount { get; }
        public decimal Open { get; internal set; }
        public int? OwnerIndex { get; }
    }

    private sealed record CashEffect(string FundCode, decimal Signed, IReadOnlyList<(DebtItem Item, decimal Amount)> Allocations);

    private readonly Dictionary<string, SampleProductSpec> _products;
    private readonly Dictionary<string, SamplePartnerSpec> _partners;
    private readonly Dictionary<(string Warehouse, string Product), decimal> _stock = new();
    private readonly Dictionary<string, decimal> _funds = new();
    private readonly Dictionary<string, DebtItem> _debts = new();
    private readonly List<DebtItem> _debtList = new();
    private readonly Dictionary<string, CashEffect> _cashEffects = new();

    private readonly bool _netExcludesVat;

    private PlanReplay(SampleDataPlan plan, bool netExcludesVat)
    {
        _netExcludesVat = netExcludesVat;
        _products = plan.Products.ToDictionary(p => p.Code);
        _partners = plan.Partners.ToDictionary(p => p.Code);
        CreditLimits = plan.CreditLimits.ToDictionary(l => l.PartnerCode, l => l.Amount);

        foreach (var stock in plan.OpeningStocks)
            foreach (var line in stock.Lines)
                AddStock(stock.WarehouseKey, line.ProductCode, line.Quantity);

        foreach (var fund in plan.Funds)
            _funds[fund.Code] = fund.OpeningAmount;

        foreach (var d in plan.OpeningDebts)
            AddDebt(new DebtItem(d.Key, d.PartnerCode, d.Side, d.Kind == OpeningDebtKind.Debt, "OD", d.DocDate, d.DueDate,
                d.Amount, d.OwnerIndex));
    }

    public IReadOnlyDictionary<(string Warehouse, string Product), decimal> Stock => _stock;
    public IReadOnlyDictionary<string, decimal> Funds => _funds;
    public IReadOnlyDictionary<string, DebtItem> Debts => _debts;
    public IReadOnlyList<DebtItem> DebtList => _debtList;
    public IReadOnlyDictionary<string, decimal> CreditLimits { get; }

    /// Amount of the last replayed step: voucher total, allocated total, transfer or offset amount, cancelled amount.
    public decimal LastAmount { get; private set; }

    /// Open invoice items of a customer (opening debts and sales): an upper bound of its credit balance.
    public decimal CreditBalance(string partnerCode) =>
        _debtList.Where(d => d.PartnerCode == partnerCode && d.Side == DebtSide.Receivable && d.IsIncrease).Sum(d => d.Open);

    /// `netExcludesVat` is the branch's inventory setting the runner computes real totals with.
    public static PlanReplay Run(SampleDataPlan plan, Action<SampleStep, PlanReplay>? afterStep = null, bool netExcludesVat = false)
    {
        var replay = new PlanReplay(plan, netExcludesVat);
        foreach (var step in plan.Steps)
        {
            replay.Apply(step);
            afterStep?.Invoke(step, replay);
        }
        return replay;
    }

    /// Estimated total of a stock voucher, as the stock voucher service computes it (no freight, no order discount).
    public static decimal EstimateTotal(SampleStockVoucherStep step, IReadOnlyDictionary<string, SampleProductSpec> products,
        bool netExcludesVat = false) =>
        StockVoucherCalculator.Compute(new StockHeaderInput(step.Type, 0m, 0m, netExcludesVat),
            step.Lines.Select(l => new StockLineInput(products[l.ProductCode].TrackInventory, PricingMode.PerUnit, false,
                null, null, null, null, l.Quantity, l.UnitPrice, l.DiscountRate, null, false, l.VatRate)).ToList()).Totals.Total;

    private void Apply(SampleStep step)
    {
        switch (step)
        {
            case SampleStockVoucherStep s:
                ApplyStock(s);
                break;
            case SampleCashVoucherStep c:
                ApplyCash(c);
                break;
            case SampleFundTransferStep t:
                MoveFund(t.FromFundCode, -t.Amount);
                MoveFund(t.ToFundCode, t.Amount);
                LastAmount = t.Amount;
                break;
            case SampleDebtOffsetStep o:
                var receivable = Debt(o.ReceivableTargetKey);
                var payable = Debt(o.PayableTargetKey);
                var offset = Math.Min(receivable.Open, payable.Open);
                receivable.Open -= offset;
                payable.Open -= offset;
                LastAmount = offset;
                break;
            case SampleCancelStep x:
                if (!_cashEffects.Remove(x.TargetKey, out var effect))
                    throw new InvalidOperationException($"{x.Key} cancels {x.TargetKey}, which is not an active cash voucher.");
                MoveFund(effect.FundCode, -effect.Signed);
                foreach (var (item, amount) in effect.Allocations)
                    item.Open += amount;
                LastAmount = Math.Abs(effect.Signed);
                break;
            default:
                throw new InvalidOperationException($"Unknown step type {step.GetType().Name}.");
        }
    }

    private void ApplyStock(SampleStockVoucherStep s)
    {
        foreach (var line in s.Lines.Where(l => _products[l.ProductCode].TrackInventory))
            AddStock(s.WarehouseKey, line.ProductCode, s.Type == StockDirection.In ? line.Quantity : -line.Quantity);

        var total = EstimateTotal(s, _products, _netExcludesVat);
        LastAmount = total;
        if (s.PartnerCode is null)
            return;

        var (side, isIncrease) = s.ReasonCode switch
        {
            "XBH" => (DebtSide.Receivable, true),
            "NTL" => (DebtSide.Receivable, false),
            "NMH" => (DebtSide.Payable, true),
            "XTL" => (DebtSide.Payable, false),
            _ => throw new InvalidOperationException($"{s.Key}: reason {s.ReasonCode} posts no debt but has a partner."),
        };
        var date = DateOnly.FromDateTime(s.At.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var item = new DebtItem(s.Key, s.PartnerCode, side, isIncrease, s.ReasonCode, date,
            isIncrease ? date.AddDays(_partners[s.PartnerCode].CreditDays ?? 0) : null, total, s.OwnerIndex);
        AddDebt(item);

        if (s.PaymentMethodCode is null)
            return;
        // The automatic receipt (sale, purchase return) or payment (purchase, sale return) settles the voucher's own item.
        item.Open = 0m;
        var moneyIn = side == DebtSide.Receivable ? isIncrease : !isIncrease;
        MoveFund(s.FundCode!, moneyIn ? total : -total);
    }

    private void ApplyCash(SampleCashVoucherStep c)
    {
        var allocations = new List<(DebtItem, decimal)>();
        decimal total;
        if (c.FixedAmount is { } fixedAmount)
        {
            total = fixedAmount;
        }
        else
        {
            total = 0m;
            foreach (var target in c.Targets)
            {
                var item = Debt(target.TargetKey);
                var amount = Math.Min(item.Open, target.MaxAmount ?? item.Open);
                item.Open -= amount;
                total += amount;
                allocations.Add((item, amount));
            }
        }

        var signed = c.Type == CashVoucherType.Receipt ? total : -total;
        MoveFund(c.FundCode, signed);
        _cashEffects[c.Key] = new CashEffect(c.FundCode, signed, allocations);
        LastAmount = total;
    }

    private DebtItem Debt(string key) =>
        _debts.TryGetValue(key, out var item) ? item : throw new InvalidOperationException($"Unknown debt target {key}.");

    private void AddDebt(DebtItem item)
    {
        _debts.Add(item.Key, item);
        _debtList.Add(item);
    }

    private void AddStock(string warehouse, string product, decimal quantity) =>
        _stock[(warehouse, product)] = _stock.GetValueOrDefault((warehouse, product)) + quantity;

    private void MoveFund(string code, decimal delta)
    {
        if (!_funds.ContainsKey(code))
            throw new InvalidOperationException($"Unknown fund {code}.");
        _funds[code] += delta;
    }
}
