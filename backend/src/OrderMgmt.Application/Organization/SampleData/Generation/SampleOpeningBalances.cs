using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Opening stock per warehouse and opening debts of the sample dataset. Draw order from `random` is part of determinism.
public static class SampleOpeningBalances
{
    private const decimal ReceivablePrepayment = 20_000_000m;
    private const decimal PayablePrepayment = 30_000_000m;

    // Base invoice totals (sum 350 million / 200 million); each is varied by up to 5 million in 500,000 steps.
    private static readonly decimal[] ReceivableBases =
        { 60_000_000m, 25_000_000m, 45_000_000m, 30_000_000m, 20_000_000m, 35_000_000m, 40_000_000m, 30_000_000m, 35_000_000m, 30_000_000m };

    private static readonly decimal[] PayableBases = { 70_000_000m, 50_000_000m, 45_000_000m, 35_000_000m };

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
                var quantity = Math.Max(1m, Math.Round(plan.WeeklySales * random.Between(minWeeks, maxWeeks), MidpointRounding.AwayFromZero));
                lines.Add(new SampleOpeningStockLine(plan.ProductCode, quantity, quantity * costs[plan.ProductCode]));
            }
            stocks.Add(new SampleOpeningStockSpec(key, lines));
        }
        return stocks;
    }

    public static List<SampleOpeningDebtSpec> BuildDebts(SampleRandom random, DateOnly openingDate, int ownerCount)
    {
        var partners = SampleCatalog.BuildPartners(ownerCount).ToDictionary(p => p.Code);
        var profiles = SampleCatalog.CustomerProfiles;
        var debts = new List<SampleOpeningDebtSpec>();

        // Customers in profile order: the bad debt one, the late payers, then the large on-time non-retail customers.
        var badDebt = profiles.First(p => p.Habit == PaymentHabit.BadDebt);
        var late = profiles.Where(p => p.Habit == PaymentHabit.Late).ToList();
        var large = profiles.Where(p => p.Habit == PaymentHabit.OnTime && p.Tier == SalesTier.Large && p.Group != CustomerGroup.Retail).ToList();

        // (customer, how many days old the invoice may be): old items for the bad debt and the late payers.
        var receivableItems = new List<(string Code, int MinAge, int MaxAge)>
        {
            (badDebt.Code, 100, 120),
            (late[0].Code, 80, 120),
            (late[0].Code, 40, 79),
            (late[1].Code, 60, 110),
        };
        for (var i = 0; i < large.Count; i++)
        {
            receivableItems.Add((large[i].Code, 10, 70));
        }
        receivableItems.Add((large[0].Code, 71, 100));

        var number = 0;
        for (var i = 0; i < receivableItems.Count; i++)
        {
            var (code, minAge, maxAge) = receivableItems[i];
            var docDate = openingDate.AddDays(-random.Between(minAge, maxAge));
            var amount = ReceivableBases[i] + random.NextAmount(-5_000_000m, 5_000_000m, 500_000m);
            debts.Add(Debt(++number, code, DebtSide.Receivable, OpeningDebtKind.Debt, $"HD-DK-{i + 1:000}", docDate,
                partners[code], amount));
        }

        var retailPrepay = profiles.First(p => p.HasPrepayment);
        debts.Add(Debt(++number, retailPrepay.Code, DebtSide.Receivable, OpeningDebtKind.Prepayment, "PT-DK-001",
            openingDate.AddDays(-random.Between(5, 30)), partners[retailPrepay.Code], ReceivablePrepayment));

        // Suppliers in the order the product plans first name them.
        var suppliers = SampleCatalog.ProductPlans.Select(p => p.PrimarySupplier).Distinct().ToList();
        var payableSuppliers = new[] { suppliers[0], suppliers[0], suppliers[1], suppliers[2] };
        for (var i = 0; i < payableSuppliers.Length; i++)
        {
            var code = payableSuppliers[i];
            var docDate = openingDate.AddDays(-random.Between(10, 90));
            var amount = PayableBases[i] + random.NextAmount(-5_000_000m, 5_000_000m, 500_000m);
            debts.Add(Debt(++number, code, DebtSide.Payable, OpeningDebtKind.Debt, $"PN-DK-{i + 1:000}", docDate,
                partners[code], amount));
        }

        debts.Add(Debt(++number, suppliers[3], DebtSide.Payable, OpeningDebtKind.Prepayment, "TU-DK-001",
            openingDate.AddDays(-random.Between(5, 30)), partners[suppliers[3]], PayablePrepayment));

        return debts;
    }

    private static SampleOpeningDebtSpec Debt(int number, string partnerCode, DebtSide side, OpeningDebtKind kind, string docNo,
        DateOnly docDate, SamplePartnerSpec partner, decimal amount)
    {
        var dueDate = kind == OpeningDebtKind.Debt ? docDate.AddDays(partner.CreditDays ?? 0) : (DateOnly?)null;
        var ownerIndex = side == DebtSide.Receivable ? partner.OwnerIndex : null;
        return new SampleOpeningDebtSpec($"OD-{number:00}", partnerCode, side, kind, docNo, docDate, dueDate, amount, ownerIndex);
    }
}
