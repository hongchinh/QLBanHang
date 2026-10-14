using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

/// What the sample dataset looks like on the day it is generated (T = Today, the day after the last document).
/// "Open at T" means after the last step of the replay.
public class SampleDataGeneratorEndStateTests
{
    private const int Owners = 3;

    /// Several codes and dates: a window ending mid-month, one with February, one of about four months, one ending mid-June.
    public static TheoryData<string, string> Inputs => new()
    {
        { "DEMO01", "2026-10-14" },
        { "DEMO02", "2026-03-01" },
        { "CN-DEMO", "2026-12-31" },
        { "DEMO03", "2026-06-15" },
    };

    /// The end-state inputs plus inputs that exercise a rule the ranges miss (chosen against the current random stream):
    /// H001 and H002 have a short till on a deposit day inside the end hold (early collection there must stay within
    /// 5 days of the due date); Y081 and Y011 need clearance to end with 3 low products.
    public static TheoryData<string, string> MechanismInputs
    {
        get
        {
            var data = Inputs;
            data.Add("H001", "2026-10-14");
            data.Add("H002", "2026-12-31");
            data.Add("Y081", "2026-01-05");
            data.Add("Y011", "2026-05-01");
            return data;
        }
    }

    private static (SampleDataPlan Plan, PlanReplay End, DateOnly T) Replay(string code, string today)
    {
        var t = SampleDataGeneratorTests.ParseDate(today);
        var plan = SampleDataGeneratorTests.Generate(code, Owners, t);
        return (plan, PlanReplay.Run(plan), t);
    }

    private static List<PlanReplay.DebtItem> OpenInvoices(PlanReplay end) =>
        end.DebtList.Where(d => d.Side == DebtSide.Receivable && d.IsIncrease && d.Open > 0).ToList();

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Monthly_sales_revenue_is_in_range(string code, string today)
    {
        var (plan, _, _) = Replay(code, today);
        var products = plan.Products.ToDictionary(p => p.Code);

        var months = SampleDataGeneratorTests.FullMonths(plan);
        months.Should().NotBeEmpty();
        foreach (var (year, month) in months)
        {
            var revenue = plan.Steps.OfType<SampleStockVoucherStep>()
                .Where(s => s.ReasonCode == "XBH")
                .Where(s => SampleDataGeneratorTests.VnDate(s) is var d && d.Year == year && d.Month == month)
                .Sum(s => PlanReplay.EstimateTotal(s, products));
            revenue.Should().BeInRange(800_000_000m, 1_200_000_000m, $"sales revenue of {year}-{month:00}");
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Open_receivables_at_T_are_in_range(string code, string today)
    {
        var (_, end, _) = Replay(code, today);

        OpenInvoices(end).Sum(d => d.Open).Should().BeInRange(400_000_000m, 600_000_000m);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Overdue_buckets_are_all_filled(string code, string today)
    {
        var (_, end, t) = Replay(code, today);

        var overdueDays = OpenInvoices(end)
            .Where(d => d.DueDate < t)
            .Select(d => t.DayNumber - d.DueDate!.Value.DayNumber)
            .ToList();

        overdueDays.Should().Contain(days => days >= 1 && days <= 30, "an item is 1–30 days overdue");
        overdueDays.Should().Contain(days => days >= 31 && days <= 60, "an item is 31–60 days overdue");
        overdueDays.Should().Contain(days => days >= 61 && days <= 90, "an item is 61–90 days overdue");
        overdueDays.Should().Contain(days => days > 90, "an item is more than 90 days overdue");
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void A_prepayment_stays_open(string code, string today)
    {
        var (_, end, _) = Replay(code, today);

        end.DebtList.Should().ContainSingle(d => d.Side == DebtSide.Receivable && !d.IsIncrease && d.Source == "OD")
            .Which.Open.Should().Be(15_000_000m);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Every_owner_has_open_debt(string code, string today)
    {
        var (_, end, _) = Replay(code, today);

        OpenInvoices(end).Select(d => d.OwnerIndex).Distinct().Should().BeEquivalentTo(new int?[] { 0, 1, 2 });
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Some_products_end_low(string code, string today)
    {
        var (_, end, _) = Replay(code, today);

        end.Stock.Values.Should().OnlyContain(q => q >= 0);
        var low = SampleCatalog.ProductPlans
            .Where(p => end.Stock.GetValueOrDefault(("K1", p.ProductCode)) + end.Stock.GetValueOrDefault(("K2", p.ProductCode))
                < p.WeeklySales)
            .Select(p => p.ProductCode)
            .ToList();
        low.Should().HaveCountGreaterThanOrEqualTo(3, "a few products end below a week of sales");
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Funds_move_every_week(string code, string today)
    {
        var (plan, _, _) = Replay(code, today);
        var cashFunds = plan.Steps.OfType<SampleCashVoucherStep>().ToDictionary(c => c.Key, c => c.FundCode);
        var touched = new Dictionary<DateOnly, HashSet<string>>();
        foreach (var step in plan.Steps)
        {
            var funds = step switch
            {
                SampleStockVoucherStep s when s.FundCode != null => new[] { s.FundCode },
                SampleCashVoucherStep c => new[] { c.FundCode },
                SampleFundTransferStep f => new[] { f.FromFundCode, f.ToFundCode },
                SampleCancelStep x => new[] { cashFunds[x.TargetKey] },
                _ => Array.Empty<string>(),
            };
            var day = SampleDataGeneratorTests.VnDate(step);
            var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            if (!touched.TryGetValue(monday, out var set))
                touched[monday] = set = new HashSet<string>();
            set.UnionWith(funds);
        }

        var first = plan.OpeningDate.AddDays(-(((int)plan.OpeningDate.DayOfWeek + 6) % 7));
        for (var monday = first; monday <= plan.LastDocumentDate; monday = monday.AddDays(7))
        {
            // A week whose working days all fall outside the window has nothing to show.
            if (SampleCalendar.WorkingDays(Max(monday, plan.OpeningDate), Min(monday.AddDays(5), plan.LastDocumentDate)).Count == 0)
                continue;
            var set = touched.GetValueOrDefault(monday) ?? new HashSet<string>();
            set.Should().Contain("QTM", $"the week of {monday:yyyy-MM-dd} moves cash");
            set.Overlaps(new[] { "VCB", "TCB" }).Should().BeTrue($"the week of {monday:yyyy-MM-dd} moves a bank fund");
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Step_count_is_reasonable(string code, string today)
    {
        var (plan, _, _) = Replay(code, today);

        plan.Steps.Count.Should().BeInRange(200, 320);
    }

    // ---- Mechanisms behind the end state: each pins one rule that the ranges above do not catch on their own ----

    [Theory]
    [MemberData(nameof(MechanismInputs))]
    public void The_late_payer_and_the_bad_debt_customer_hold_credit_sales_placed_relative_to_T(string code, string today)
    {
        var (_, end, t) = Replay(code, today);

        foreach (var (habit, overdueDays) in new[]
                 {
                     (PaymentHabit.Late, SampleDataVolumes.LateSaleOverdueDays),
                     (PaymentHabit.BadDebt, SampleDataVolumes.BadDebtSaleOverdueDays),
                 })
        {
            var customer = SampleCatalog.CustomerProfiles.First(p => p.Habit == habit).Code;
            // A sale placed on a Sunday moves to the Monday, one day less overdue.
            end.DebtList.Where(d => d.PartnerCode == customer && d.Source == "XBH" && d.Open > 0)
                .Select(d => t.DayNumber - d.DueDate!.Value.DayNumber)
                .Should().Contain(days => days == overdueDays || days == overdueDays - 1,
                    $"{customer} keeps a sale due {overdueDays} days before T open");
        }
    }

    [Theory]
    [MemberData(nameof(MechanismInputs))]
    public void The_end_hold_settles_nothing_early(string code, string today)
    {
        var (plan, end, _) = Replay(code, today);
        var holdStart = plan.LastDocumentDate.AddDays(1 - SampleDataVolumes.EndHoldDays);
        var dueDates = end.DebtList.ToDictionary(d => d.Key, d => d.DueDate);

        foreach (var receipt in plan.Steps.OfType<SampleCashVoucherStep>()
                     .Where(c => c.ReasonCode == "TKH" && SampleDataGeneratorTests.VnDate(c) >= holdStart))
        {
            var date = SampleDataGeneratorTests.VnDate(receipt);
            // On-time customers pay up to 5 days before the due date; nothing later than that is settled during the hold.
            receipt.Targets.Should().OnlyContain(target => dueDates[target.TargetKey] <= date.AddDays(5),
                $"{receipt.Key} on {date:yyyy-MM-dd} settles only invoices due by then");
        }
    }

    /// The end-low rule (no purchase in the last EndLowDays days) together with clearance (those products go with the
    /// sales until they run out). Y081 / 2026-01-05 and Y011 / 2026-05-01 end with fewer than 3 such products without
    /// clearance.
    [Theory]
    [MemberData(nameof(MechanismInputs))]
    public void A_few_products_are_no_longer_bought_and_end_low(string code, string today)
    {
        var (plan, end, _) = Replay(code, today);

        EndLowProducts(plan, end).Should().HaveCountGreaterThanOrEqualTo(3,
            $"a few products get no purchase in the last {SampleDataVolumes.EndLowDays} days and end below a week of sales");
    }

    /// Products with no purchase line in the last EndLowDays days that end the window below a week of sales.
    private static HashSet<string> EndLowProducts(SampleDataPlan plan, PlanReplay end)
    {
        var from = plan.LastDocumentDate.AddDays(-SampleDataVolumes.EndLowDays);
        var bought = plan.Steps.OfType<SampleStockVoucherStep>()
            .Where(s => s.ReasonCode == "NMH" && SampleDataGeneratorTests.VnDate(s) >= from)
            .SelectMany(s => s.Lines.Select(l => l.ProductCode))
            .ToHashSet();
        return SampleCatalog.ProductPlans
            .Where(p => !bought.Contains(p.ProductCode)
                && end.Stock.GetValueOrDefault(("K1", p.ProductCode)) + end.Stock.GetValueOrDefault(("K2", p.ProductCode)) < p.WeeklySales)
            .Select(p => p.ProductCode)
            .ToHashSet();
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
