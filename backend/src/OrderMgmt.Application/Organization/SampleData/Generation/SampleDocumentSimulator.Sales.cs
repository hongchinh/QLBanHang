using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using static OrderMgmt.Application.Organization.SampleData.Generation.SampleDataVolumes;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Sales (XBH): customer, lines, revenue pacing and payment, including the sales desk's receivables band.
internal sealed partial class SampleDocumentSimulator
{
    /// Planned product picks per week over all sales: sales per week × average goods lines per sale.
    private static readonly decimal PicksPerWeek =
        (SalesPerMonthMin + SalesPerMonthMax) / 2m * 12m / 52m * (SaleLinesMin + SaleLinesMax) / 2m;

    private readonly List<(DateOnly Date, SampleStockVoucherStep Step)> _sales = new();
    private bool _receivablesReachedFloor;

    private void Sale()
    {
        // Planned credit sales come first (placed relative to T for the overdue buckets, or for an owner's open invoice at
        // T), then, shortly before the debt offset, one to the dual-role partner if it has no open one. A planned sale is
        // taken off the plan only once it is made.
        var plannedIndex = _plannedSales.FindIndex(s => s.Date <= _date);
        var plannedSale = plannedIndex >= 0 ? _plannedSales[plannedIndex] : ((DateOnly Date, SampleCustomerProfile Customer, bool Hold)?)null;
        var customer = plannedSale?.Customer ?? (OffsetNeedsSale() ? _profiles[DualRolePartner] : PickCustomer());
        var plannedCredit = plannedSale != null || customer.Code == DualRolePartner && OffsetNeedsSale();
        var warehouse = _random.Chance(K1SalePercent) ? K1 : K2;
        var available = InStock(warehouse);
        if (available.Count == 0)
        {
            warehouse = warehouse == K1 ? K2 : K1;
            available = InStock(warehouse);
            if (available.Count == 0)
                return;
        }
        if (plannedIndex >= 0)
            _plannedSales.RemoveAt(plannedIndex);

        // A line sells about a week of the product's demand in this warehouse divided by how often the product is picked
        // there (picks per week × warehouse share / products in stock), so planned and simulated sales agree. The month's
        // revenue pacing then scales every line of the sale alike, which keeps the tiers' proportions.
        var discount = DiscountRate(customer.Group);
        var tier = customer.Tier switch
        {
            SalesTier.Large => LargeQuantityPercent,
            SalesTier.Small => SmallQuantityPercent,
            _ => MediumQuantityPercent,
        };
        var pace = RevenuePace();
        var lines = new List<SampleStockLine>();
        foreach (var product in PickSaleProducts(available, singleLine: plannedSale != null))
        {
            var planned = product.WeeklySales * DemandPercent / 100m * available.Count / PicksPerWeek * tier / 100m;
            var spread = 1m + ((decimal)_random.NextDouble() * 2m - 1m) * SaleQuantitySpreadPercent / 100m;
            var quantity = Math.Clamp(Math.Round(planned * spread * pace, MidpointRounding.AwayFromZero), 1m,
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
        var (method, fund) = SalePayment(customer, total, plannedCredit);
        var step = new SampleStockVoucherStep(_ledger.NextKey("XBH"), default, OwnerOf(customer.Code), StockDirection.Out, "XBH",
            warehouse, customer.Code, method, fund, null, lines);
        Write(step);
        foreach (var line in lines)
            _ledger.AddStock(warehouse, line.ProductCode, -line.Quantity);
        _sales.Add((_date, step));
        Month.SalesDone++;
        Month.Revenue += total;

        if (fund != null)
        {
            _ledger.MoveFund(fund, total);
            return;
        }
        var due = _date.AddDays(_partners[customer.Code].CreditDays ?? 0);
        var payDate = CollectionDate(customer, due, opening: false)!.Value;
        _ledger.Items.Add(new SampleOpenItem(step.Key, customer.Code, DebtSide.Receivable, "XBH", false, total, due,
            payDate > _date ? payDate : _date.AddDays(1)));
        if (plannedSale?.Hold == true)
            _heldKeys.Add(step.Key);
    }

    /// 1–6 distinct products in stock; a planned credit sale has a single line, so it stays small next to the receivables
    /// ceiling it bypasses. While the end-low products are run down, any other sale that can includes one of them
    /// (clearance), as an extra line when it has room for one, so they really sell out.
    private List<SampleProductPlan> PickSaleProducts(List<SampleProductPlan> available, bool singleLine)
    {
        var lineCount = _random.Between(SaleLinesMin, SaleLinesMax);
        var picked = PickDistinct(available, Math.Min(singleLine ? 1 : lineCount, available.Count));
        var endLow = available.Where(IsEndLow).ToList();
        if (singleLine || endLow.Count == 0 || picked.Any(IsEndLow))
            return picked;
        var clearance = _random.Pick(endLow);
        if (picked.Count < SaleLinesMax)
            picked.Add(clearance);
        else
            picked[0] = clearance;
        return picked;
    }

    /// Scale of the next sale's quantities: 1, corrected in proportion to how far the month's revenue is behind (or ahead
    /// of) its target for the sales made so far.
    private decimal RevenuePace()
    {
        var month = Month;
        var expected = month.RevenueTarget * month.SalesDone / month.PlannedSales;
        var pace = 1m + RevenueGain * (expected - month.Revenue) / month.RevenueTarget;
        return Math.Clamp(pace, SalePaceMinPercent / 100m, SalePaceMaxPercent / 100m);
    }

    private SampleCustomerProfile PickCustomer()
    {
        var roll = _random.Between(1, 100);
        var tier = roll <= LargeTierPercent ? SalesTier.Large
            : roll <= LargeTierPercent + SmallTierPercent ? SalesTier.Small
            : SalesTier.Medium;
        return _random.Pick(SampleCatalog.CustomerProfiles.Where(p => p.Tier == tier).ToList());
    }

    /// Retail pays cash at once; others pay at once by chance, else buy on credit. A planned credit sale is on credit.
    /// A sale that would take a customer over 80% of its limit is paid by bank. On the deposit day, while the till is
    /// short, sales are paid in cash, so the week ends with a deposit. The sales desk keeps the company's open receivables
    /// in [ReceivableFloor, ReceivableCeiling]: above the ceiling it asks for payment at once; below the floor (once the
    /// receivables have reached it, so not while they build up after the opening) it sells on credit. During the end hold
    /// it steers the receivables expected at T: the open ones less those to be collected on their date before T.
    private (string? Method, string? Fund) SalePayment(SampleCustomerProfile customer, decimal total, bool plannedCredit)
    {
        var open = OpenReceivables();
        _receivablesReachedFloor |= open >= ReceivableFloor;
        if (_holdStarted)
        {
            open -= _ledger.Items.Where(i => i.Side == DebtSide.Receivable && !i.IsPrepayment && i.Open > 0
                && !_heldKeys.Contains(i.Key) && i.PayDate <= LastWorkingDay).Sum(i => i.Open);
        }
        if (customer.Group == CustomerGroup.Retail)
            return ("TM", CashFund);

        var paidAtOnce = _random.Chance(PaidSalePercent);
        var byBank = _random.Chance(PaidSaleBankPercent);
        var overLimit = customer.CreditLimit is { } limit
            && _ledger.OpenReceivable(customer.Code) + total > limit * CreditLimitUsePercent / 100m;
        if (plannedCredit)
            return overLimit ? ("CK", _random.Pick(BankFunds)) : (null, null);
        if (DepositIsShort())
            return ("TM", CashFund);
        if (overLimit)
            return ("CK", _random.Pick(BankFunds));
        if (paidAtOnce && (open >= ReceivableFloor || !_receivablesReachedFloor) || open + total > ReceivableCeiling)
            return byBank ? ("CK", _random.Pick(BankFunds)) : ("TM", CashFund);
        return (null, null);
    }

    /// The last working day of the window: the last document date, or the Saturday before it when that is a Sunday.
    private DateOnly LastWorkingDay => _plan.LastDocumentDate.DayOfWeek == DayOfWeek.Sunday
        ? _plan.LastDocumentDate.AddDays(-1)
        : _plan.LastDocumentDate;

    /// Open receivable invoices of all customers.
    private decimal OpenReceivables() =>
        _ledger.Items.Where(i => i.Side == DebtSide.Receivable && !i.IsPrepayment).Sum(i => i.Open);

    private static decimal DiscountRate(CustomerGroup group) => group switch
    {
        CustomerGroup.Agent => 5m,
        CustomerGroup.Project => 3m,
        CustomerGroup.Company => 2m,
        _ => 0m,
    };
}
