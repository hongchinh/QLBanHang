using FluentAssertions;
using FluentAssertions.Execution;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

/// The rules behind the end state that the ranges alone do not pin, checked over a fixed set of inputs wide enough that
/// a change of the random stream does not leave a rule unexercised.
public class SampleDataGeneratorMechanismTests
{
    private const decimal ShortTill = SampleDataVolumes.CashKeep + SampleDataVolumes.CashDepositMin + SampleDataVolumes.CashFloat;

    /// 45 inputs: 5 codes on each of 9 dates (windows ending on a Sunday, at the end of February, on a Thursday, of
    /// about four months, and a leap February). Most dates end on a Tuesday or Wednesday, where a short till on the last
    /// day, inside the end hold, is most frequent.
    private static IEnumerable<(string Code, DateOnly Today)> Inputs()
    {
        var dates = new[]
        {
            "2026-01-05", "2026-03-01", "2026-06-15", "2026-07-31", "2026-10-14", "2026-12-31", "2027-06-02", "2026-09-30",
            "2028-03-01",
        };
        foreach (var date in dates)
            for (var i = 0; i < 5; i++)
                yield return ($"M{i:00}", SampleDataGeneratorTests.ParseDate(date));
    }

    [Fact]
    public void End_of_window_rules_hold_over_many_inputs()
    {
        int inputs = 0, eligibleSales = 0, carryingSales = 0, inputsWithShortTillInHold = 0, endingBelowFloor = 0;
        using (new AssertionScope())
        {
            foreach (var (code, today) in Inputs())
            {
                var plan = SampleDataGeneratorTests.Generate(code, 3, today);
                var input = $"{code} {today:yyyy-MM-dd}";
                var observed = Replay(plan);

                AssertEarlyLimitedSales(plan, input);
                AssertPlacedSales(observed.End, today, input);
                AssertHold(plan, observed.End, input);
                AssertNoGatheringPastT(plan, observed.End, input);
                observed.EndLow.Should().HaveCountGreaterThanOrEqualTo(3,
                    $"{input}: a few products get no purchase in the last {SampleDataVolumes.EndLowDays} days and end low");

                inputs++;
                eligibleSales += observed.Eligible;
                carryingSales += observed.Carrying;
                if (observed.ShortTillInHold)
                    inputsWithShortTillInHold++;
                var openAtT = observed.End.DebtList.Where(d => d.Side == DebtSide.Receivable && d.IsIncrease).Sum(d => d.Open);
                if (openAtT < SampleDataVolumes.ReceivableFloor)
                    endingBelowFloor++;
            }
        }

        // Clearance: while an end-low product is in stock in the sale's warehouse, the sale carries one (about 0.8; random
        // picks alone give about 0.5).
        eligibleSales.Should().BeGreaterThan(100);
        ((double)carryingSales / eligibleSales).Should().BeGreaterThan(0.7, "clearance puts an end-low product in most late sales");
        // During the end hold the desk steers the receivables expected at T, so few inputs end under the band's floor
        // (about 1 in 10; steering the current receivables instead leaves about 1 in 3 there).
        ((double)endingBelowFloor / inputs).Should().BeLessThanOrEqualTo(0.2, "the receivables at T end within the band");
        // Coverage: the end hold's early collection rule is exercised (a short till on a deposit day inside the hold).
        inputsWithShortTillInHold.Should().BeGreaterThanOrEqualTo(2, "some inputs have a short till inside the end hold");
    }

    /// Each credit-limit customer gets a single-line credit sale (no payment method) planned LimitedCustomerSaleDay days
    /// after the opening date (the Monday after when that day is a Sunday), so it always has credit sales whatever the
    /// stream. A planned sale takes one of the day's sales and a day may have only one, so these are the first sales from
    /// that day on: the first on that day, the second on that day or the next working day.
    private static void AssertEarlyLimitedSales(SampleDataPlan plan, string input)
    {
        var early = plan.OpeningDate.AddDays(SampleDataVolumes.LimitedCustomerSaleDay);
        if (early.DayOfWeek == DayOfWeek.Sunday)
            early = early.AddDays(1);
        var nextWorkingDay = early.AddDays(early.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);
        var customers = plan.CreditLimits.Select(c => c.PartnerCode).ToList();
        customers.Should().HaveCount(2, $"{input}: two customers have a credit limit");

        var first = plan.Steps.OfType<SampleStockVoucherStep>()
            .Where(s => s.ReasonCode == "XBH" && SampleDataGeneratorTests.VnDate(s) >= early)
            .Take(customers.Count)
            .ToList();
        first.Select(s => s.PartnerCode).Should().BeEquivalentTo(customers,
            $"{input}: the first sales from {early:yyyy-MM-dd} on go to the credit-limit customers");
        first.Should().OnlyContain(s => s.PaymentMethodCode == null
                && s.Lines.Count(l => l.ProductCode != SampleCatalog.TransportProductCode) == 1,
            $"{input}: each is a single-line credit sale");
        var dates = first.Select(SampleDataGeneratorTests.VnDate).ToList();
        dates.Should().StartWith(early, $"{input}: the first is made on {early:yyyy-MM-dd}")
            .And.OnlyContain(d => d == early || d == nextWorkingDay, $"{input}: the second by {nextWorkingDay:yyyy-MM-dd}");
    }

    /// An open sale of the first late payer due LateSaleOverdueDays before T, and one of the bad debt customer due
    /// BadDebtSaleOverdueDays before T (a day less when placed on a Sunday).
    private static void AssertPlacedSales(PlanReplay end, DateOnly t, string input)
    {
        foreach (var (habit, overdueDays) in new[]
                 {
                     (PaymentHabit.Late, SampleDataVolumes.LateSaleOverdueDays),
                     (PaymentHabit.BadDebt, SampleDataVolumes.BadDebtSaleOverdueDays),
                 })
        {
            var customer = SampleCatalog.PlacedSaleCustomer(habit).Code;
            end.DebtList.Where(d => d.PartnerCode == customer && d.Source == "XBH" && d.Open > 0)
                .Select(d => t.DayNumber - d.DueDate!.Value.DayNumber)
                .Should().Contain(days => days == overdueDays || days == overdueDays - 1,
                    $"{input}: {customer} keeps a sale due {overdueDays} days before T open");
        }
    }

    /// During the end hold, receipts settle only invoices due within OnTimeDaysBeforeDueMax days of the receipt.
    private static void AssertHold(SampleDataPlan plan, PlanReplay end, string input)
    {
        var holdStart = plan.LastDocumentDate.AddDays(1 - SampleDataVolumes.EndHoldDays);
        var dueDates = end.DebtList.ToDictionary(d => d.Key, d => d.DueDate);
        foreach (var receipt in plan.Steps.OfType<SampleCashVoucherStep>()
                     .Where(c => c.ReasonCode == "TKH" && SampleDataGeneratorTests.VnDate(c) >= holdStart))
        {
            var date = SampleDataGeneratorTests.VnDate(receipt);
            receipt.Targets.Should().OnlyContain(
                target => dueDates[target.TargetKey] <= date.AddDays(SampleDataVolumes.OnTimeDaysBeforeDueMax),
                $"{input}: {receipt.Key} on {date:yyyy-MM-dd} settles only invoices due within "
                + $"{SampleDataVolumes.OnTimeDaysBeforeDueMax} days");
        }
    }

    /// Gathering never takes an invoice that would still be open at T. On a day that is not a deposit day (no early
    /// collection for the till), a receipt settles only invoices due by the last working day + OnTimeDaysBeforeDueMax,
    /// as their pay date then falls by the last working day.
    private static void AssertNoGatheringPastT(SampleDataPlan plan, PlanReplay end, string input)
    {
        var lastWorkingDay = plan.LastDocumentDate.DayOfWeek == DayOfWeek.Sunday
            ? plan.LastDocumentDate.AddDays(-1)
            : plan.LastDocumentDate;
        var dueDates = end.DebtList.ToDictionary(d => d.Key, d => d.DueDate);
        foreach (var receipt in plan.Steps.OfType<SampleCashVoucherStep>().Where(c => c.ReasonCode == "TKH"))
        {
            var date = SampleDataGeneratorTests.VnDate(receipt);
            if (date.DayOfWeek == DayOfWeek.Saturday || date == plan.LastDocumentDate)
                continue;
            receipt.Targets.Should().OnlyContain(
                target => dueDates[target.TargetKey] <= lastWorkingDay.AddDays(SampleDataVolumes.OnTimeDaysBeforeDueMax),
                $"{input}: {receipt.Key} on {date:yyyy-MM-dd} gathers no invoice still open at T");
        }
    }

    private sealed record Observed(PlanReplay End, HashSet<string> EndLow, int Eligible, int Carrying, bool ShortTillInHold);

    /// Replays the plan: the end-low products (no purchase line in the last EndLowDays days, ending below a week of
    /// sales), the late sales that could and did carry one, and whether a deposit day inside the hold started short.
    private static Observed Replay(SampleDataPlan plan)
    {
        var lowFrom = plan.LastDocumentDate.AddDays(-SampleDataVolumes.EndLowDays);
        var holdStart = plan.LastDocumentDate.AddDays(1 - SampleDataVolumes.EndHoldDays);
        var lateSales = new List<(SampleStockVoucherStep Sale, Dictionary<(string, string), decimal> StockBefore)>();
        var tillBeforeCash = new Dictionary<DateOnly, decimal>();
        var qtm = plan.Funds.Single(f => f.Code == "QTM").OpeningAmount;

        var end = PlanReplay.Run(plan, (step, replay) =>
        {
            var day = SampleDataGeneratorTests.VnDate(step);
            var depositDay = day.DayOfWeek == DayOfWeek.Saturday || day == plan.LastDocumentDate;
            // The till before the day's first receipt or deposit.
            if (depositDay && !tillBeforeCash.ContainsKey(day)
                && step is SampleCashVoucherStep { ReasonCode: "TKH" } or SampleFundTransferStep { FromFundCode: "QTM" })
                tillBeforeCash[day] = qtm;
            if (step is SampleStockVoucherStep { ReasonCode: "XBH" } sale && day >= lowFrom)
            {
                // Stock before the sale: what is left after it plus what it sold.
                var before = replay.Stock.ToDictionary(kv => kv.Key, kv => kv.Value);
                foreach (var line in sale.Lines.Where(l => before.ContainsKey((sale.WarehouseKey, l.ProductCode))))
                    before[(sale.WarehouseKey, line.ProductCode)] += line.Quantity;
                lateSales.Add((sale, before));
            }
            qtm = replay.Funds["QTM"];
        });

        var bought = plan.Steps.OfType<SampleStockVoucherStep>()
            .Where(s => s.ReasonCode == "NMH" && SampleDataGeneratorTests.VnDate(s) >= lowFrom)
            .SelectMany(s => s.Lines.Select(l => l.ProductCode))
            .ToHashSet();
        var endLow = SampleCatalog.ProductPlans
            .Where(p => !bought.Contains(p.ProductCode)
                && end.Stock.GetValueOrDefault(("K1", p.ProductCode)) + end.Stock.GetValueOrDefault(("K2", p.ProductCode)) < p.WeeklySales)
            .Select(p => p.ProductCode)
            .ToHashSet();

        int eligible = 0, carrying = 0;
        foreach (var (sale, before) in lateSales)
        {
            if (!endLow.Any(p => before.GetValueOrDefault((sale.WarehouseKey, p)) >= 1m))
                continue;
            eligible++;
            if (sale.Lines.Any(l => endLow.Contains(l.ProductCode)))
                carrying++;
        }

        var shortTillInHold = tillBeforeCash.Any(kv => kv.Key >= holdStart && kv.Value < ShortTill);
        return new Observed(end, endLow, eligible, carrying, shortTillInHold);
    }
}
