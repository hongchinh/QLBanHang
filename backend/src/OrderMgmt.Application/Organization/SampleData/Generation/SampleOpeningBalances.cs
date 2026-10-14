using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Opening stock per warehouse and opening debts of the sample dataset. Draw order from `random` is part of determinism.
public static class SampleOpeningBalances
{
    private const decimal ReceivablePrepayment = 20_000_000m;
    private const decimal PayablePrepayment = 30_000_000m;

    /// A prepayment was received (or made) this many days before the opening date.
    private const int PrepaymentAgeMin = 5;
    private const int PrepaymentAgeMax = 30;

    /// Every opening amount varies from its base by up to AmountSpread, in AmountStep steps.
    private const decimal AmountSpread = 5_000_000m;
    private const decimal AmountStep = 500_000m;

    /// One opening item: invoice aged MinAge..MaxAge days before the opening date.
    /// OverdueAtToday instead ages it from Today, so the item is that many days overdue at Today (kept inside MinAge..MaxAge).
    private sealed record OpeningRow(string Code, int MinAge, int MaxAge, decimal BaseAmount, int? OverdueAtToday = null);

    // Ages follow the payment habit, so receipts fall inside the window: on time ≤ CreditDays, late ≤ CreditDays + 30.
    // KH20 (bad debt, 45 days) is never collected: one old item and one item 75 days (up to 87) overdue at Today.
    // Bases sum to 350 million.
    private static readonly OpeningRow[] ReceivableRows =
    {
        new("DM-KH20", 100, 120, 60_000_000m),
        new("DM-KH20", 10, 120, 25_000_000m, OverdueAtToday: 75),
        new("DM-KH06", 40, 60, 45_000_000m),
        new("DM-KH11", 55, 75, 30_000_000m),
        new("DM-KH01", 10, 30, 20_000_000m),
        new("DM-KH02", 10, 30, 35_000_000m),
        new("DM-KH03", 10, 30, 40_000_000m),
        new("DM-KH08", 10, 30, 30_000_000m),
        new("DM-KH10", 10, 45, 35_000_000m),
        new("DM-KH01", 10, 30, 30_000_000m),
    };

    // Payables are paid near their due date, so they are not yet due at opening: age ≤ CreditDays. Bases sum to 200 million.
    private static readonly OpeningRow[] PayableRows =
    {
        new("DM-NCC01", 10, 30, 70_000_000m),
        new("DM-NCC01", 10, 30, 50_000_000m),
        new("DM-NCC02", 10, 30, 45_000_000m),
        new("DM-NCC03", 10, 15, 35_000_000m),
    };

    private const string PayablePrepaymentSupplier = "DM-NCC04";

    /// The retail customer flagged HasPrepayment in the catalog.
    private static string ReceivablePrepaymentCustomer => SampleCatalog.CustomerProfiles.Single(p => p.HasPrepayment).Code;

    public static List<SampleOpeningStockSpec> BuildStocks(SampleRandom random)
    {
        var plans = SampleCatalog.ProductPlans;
        var costs = SampleCatalog.Products.ToDictionary(p => p.Code, p => p.CostPrice);
        var stocks = new List<SampleOpeningStockSpec>();

        foreach (var key in new[] { "K1", "K2" })
        {
            var lines = new List<SampleOpeningStockLine>();
            foreach (var plan in plans.Where(p => key == "K1" ? p.InK1 : p.InK2))
            {
                var (minWeeks, maxWeeks) = key == "K1"
                    ? (SampleDataVolumes.OpeningWeeksK1Min, SampleDataVolumes.OpeningWeeksK1Max)
                    : plan.InK1
                        ? (SampleDataVolumes.OpeningWeeksK2SharedMin, SampleDataVolumes.OpeningWeeksK2SharedMax)
                        : (SampleDataVolumes.OpeningWeeksK2OnlyMin, SampleDataVolumes.OpeningWeeksK2OnlyMax);
                var weeks = minWeeks + (decimal)random.NextDouble() * (maxWeeks - minWeeks);
                var quantity = Math.Max(1m, Math.Round(plan.WeeklySales * weeks, MidpointRounding.AwayFromZero));
                lines.Add(new SampleOpeningStockLine(plan.ProductCode, quantity, quantity * costs[plan.ProductCode]));
            }
            stocks.Add(new SampleOpeningStockSpec(key, lines));
        }
        return stocks;
    }

    public static List<SampleOpeningDebtSpec> BuildDebts(SampleRandom random, DateOnly openingDate, DateOnly today,
        IReadOnlyList<SamplePartnerSpec> partnerList)
    {
        var partners = partnerList.ToDictionary(p => p.Code);
        var daysToToday = today.DayNumber - openingDate.DayNumber;
        var debts = new List<SampleOpeningDebtSpec>();
        var number = 0;

        for (var i = 0; i < ReceivableRows.Length; i++)
        {
            var row = ReceivableRows[i];
            var partner = partners[row.Code];
            var docDate = openingDate.AddDays(-Age(random, row, partner, daysToToday));
            debts.Add(OpeningItem(++number, partner, DebtSide.Receivable, OpeningDebtKind.Debt, $"HD-DK-{i + 1:000}", docDate,
                Amount(random, row)));
        }

        debts.Add(OpeningItem(++number, partners[ReceivablePrepaymentCustomer], DebtSide.Receivable, OpeningDebtKind.Prepayment,
            "PT-DK-001", openingDate.AddDays(-random.Between(PrepaymentAgeMin, PrepaymentAgeMax)), ReceivablePrepayment));

        for (var i = 0; i < PayableRows.Length; i++)
        {
            var row = PayableRows[i];
            var partner = partners[row.Code];
            var docDate = openingDate.AddDays(-Age(random, row, partner, daysToToday));
            debts.Add(OpeningItem(++number, partner, DebtSide.Payable, OpeningDebtKind.Debt, $"PN-DK-{i + 1:000}", docDate,
                Amount(random, row)));
        }

        debts.Add(OpeningItem(++number, partners[PayablePrepaymentSupplier], DebtSide.Payable, OpeningDebtKind.Prepayment,
            "TU-DK-001", openingDate.AddDays(-random.Between(PrepaymentAgeMin, PrepaymentAgeMax)), PayablePrepayment));

        return debts;
    }

    // overdue at Today = daysToToday + age - CreditDays, so age = overdue - daysToToday + CreditDays.
    private static int Age(SampleRandom random, OpeningRow row, SamplePartnerSpec partner, int daysToToday) =>
        row.OverdueAtToday is { } overdue
            ? Math.Clamp(overdue - daysToToday + (partner.CreditDays ?? 0), row.MinAge, row.MaxAge)
            : random.Between(row.MinAge, row.MaxAge);

    private static decimal Amount(SampleRandom random, OpeningRow row) =>
        row.BaseAmount + random.NextAmount(-AmountSpread, AmountSpread, AmountStep);

    private static SampleOpeningDebtSpec OpeningItem(int number, SamplePartnerSpec partner, DebtSide side, OpeningDebtKind kind,
        string docNo, DateOnly docDate, decimal amount)
    {
        var dueDate = kind == OpeningDebtKind.Debt ? docDate.AddDays(partner.CreditDays ?? 0) : (DateOnly?)null;
        var ownerIndex = side == DebtSide.Receivable ? partner.OwnerIndex : null;
        return new SampleOpeningDebtSpec($"OD-{number:00}", partner.Code, side, kind, docNo, docDate, dueDate, amount, ownerIndex);
    }
}
