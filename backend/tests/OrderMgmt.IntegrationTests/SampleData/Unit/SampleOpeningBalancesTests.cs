using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleOpeningBalancesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static SampleDataPlan Generate(int ownerCount = 3, DateOnly? today = null) =>
        new SampleDataGenerator().Generate(new SampleDataGeneratorInput("DEMO01", today ?? Today, ownerCount));

    private static Dictionary<string, SamplePartnerSpec> PartnersByCode(SampleDataPlan plan) =>
        plan.Partners.ToDictionary(p => p.Code);

    private static Dictionary<string, decimal> WeeklySales() =>
        SampleCatalog.ProductPlans.ToDictionary(p => p.ProductCode, p => p.WeeklySales);

    private static IReadOnlyList<SampleOpeningStockLine> Lines(SampleDataPlan plan, string key) =>
        plan.OpeningStocks.Single(s => s.WarehouseKey == key).Lines;

    [Theory]
    [InlineData("K1", 26)]
    [InlineData("K2", 10)]
    public void Opening_stock_has_the_tracked_products_of_the_warehouse(string key, int expectedLines)
    {
        var lines = Lines(Generate(), key);

        lines.Should().HaveCount(expectedLines);
        lines.Select(l => l.ProductCode).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Opening_stock_lines_are_whole_quantities_priced_at_cost()
    {
        var plan = Generate();
        var products = plan.Products.ToDictionary(p => p.Code);

        plan.OpeningStocks.Select(s => s.WarehouseKey).Should().BeEquivalentTo(new[] { "K1", "K2" });
        foreach (var line in plan.OpeningStocks.SelectMany(s => s.Lines))
        {
            var product = products[line.ProductCode];
            product.TrackInventory.Should().BeTrue();
            line.Quantity.Should().BeGreaterThan(0);
            line.Quantity.Should().Be(decimal.Truncate(line.Quantity));
            line.Amount.Should().Be(line.Quantity * product.CostPrice);
        }
    }

    [Fact]
    public void K1_opens_with_three_to_six_weeks_of_sales()
    {
        var weekly = WeeklySales();

        foreach (var line in Lines(Generate(), "K1"))
        {
            line.Quantity.Should().BeInRange(Math.Round(3 * weekly[line.ProductCode]), Math.Round(6 * weekly[line.ProductCode]),
                line.ProductCode);
        }
    }

    [Fact]
    public void K2_products_also_in_k1_open_with_one_to_three_weeks_and_k2_only_ones_with_three_to_six()
    {
        var weekly = WeeklySales();
        var plans = SampleCatalog.ProductPlans.ToDictionary(p => p.ProductCode);
        var lines = Lines(Generate(), "K2");

        lines.Should().Contain(l => plans[l.ProductCode].InK1).And.Contain(l => !plans[l.ProductCode].InK1);
        foreach (var line in lines)
        {
            var (min, max) = plans[line.ProductCode].InK1 ? (1, 3) : (3, 6);
            line.Quantity.Should().BeInRange(Math.Round(min * weekly[line.ProductCode]), Math.Round(max * weekly[line.ProductCode]),
                line.ProductCode);
        }
    }

    [Fact]
    public void Opening_quantities_are_not_whole_weeks_of_sales()
    {
        var weekly = WeeklySales();

        Generate().OpeningStocks.SelectMany(s => s.Lines)
            .Count(l => l.Quantity % weekly[l.ProductCode] != 0)
            .Should().BeGreaterThan(20);
    }

    [Fact]
    public void Opening_debts_have_sixteen_unique_keys_in_order()
    {
        var debts = Generate().OpeningDebts;

        debts.Should().HaveCount(16);
        debts.Select(d => d.Key).Should().Equal(Enumerable.Range(1, 16).Select(i => $"OD-{i:00}"));
        debts.Select(d => d.DocNo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Opening_debts_have_the_specified_shape()
    {
        var plan = Generate();
        var partners = PartnersByCode(plan);
        var debts = plan.OpeningDebts;

        var receivables = debts.Where(d => d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt).ToList();
        receivables.Should().HaveCount(10);
        receivables.Select(d => d.PartnerCode).Distinct().Should().HaveCount(8);
        receivables.Should().OnlyContain(d => partners[d.PartnerCode].Group != CustomerGroup.Retail);

        var payables = debts.Where(d => d.Side == DebtSide.Payable && d.Kind == OpeningDebtKind.Debt).ToList();
        payables.Should().HaveCount(4);
        payables.Select(d => d.PartnerCode).Distinct().Should().HaveCount(3);

        var recvPrepay = debts.Single(d => d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Prepayment);
        partners[recvPrepay.PartnerCode].Group.Should().Be(CustomerGroup.Retail);
        recvPrepay.Amount.Should().Be(20_000_000m);

        var payPrepay = debts.Single(d => d.Side == DebtSide.Payable && d.Kind == OpeningDebtKind.Prepayment);
        payPrepay.Amount.Should().Be(30_000_000m);
    }

    [Fact]
    public void Opening_receivables_belong_to_the_bad_debt_the_late_and_the_large_on_time_customers()
    {
        var profiles = SampleCatalog.CustomerProfiles.ToDictionary(p => p.Code);
        var receivables = Generate().OpeningDebts
            .Where(d => d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt)
            .Select(d => profiles[d.PartnerCode])
            .ToList();

        receivables.Count(p => p.Habit == PaymentHabit.BadDebt).Should().Be(2);
        receivables.Where(p => p.Habit == PaymentHabit.Late).Select(p => p.Code)
            .Should().BeEquivalentTo(SampleCatalog.CustomerProfiles.Where(p => p.Habit == PaymentHabit.Late).Select(p => p.Code));
        receivables.Where(p => p.Habit == PaymentHabit.OnTime)
            .Should().OnlyContain(p => p.Tier == SalesTier.Large && p.Group != CustomerGroup.Retail);
        receivables.Where(p => p.Habit == PaymentHabit.OnTime).Select(p => p.Code).Distinct()
            .Should().HaveCount(SampleCatalog.CustomerProfiles.Count(p =>
                p.Habit == PaymentHabit.OnTime && p.Tier == SalesTier.Large && p.Group != CustomerGroup.Retail));
    }

    [Fact]
    public void Opening_debt_dates_and_amounts_are_consistent()
    {
        var plan = Generate();
        var partners = PartnersByCode(plan);

        foreach (var d in plan.OpeningDebts)
        {
            d.DocDate.Should().BeOnOrBefore(plan.OpeningDate);
            d.Amount.Should().BeGreaterThan(0);
            if (d.Kind == OpeningDebtKind.Debt)
            {
                d.DocDate.Should().BeOnOrAfter(plan.OpeningDate.AddDays(-120), d.Key);
                d.DocDate.Should().BeOnOrBefore(plan.OpeningDate.AddDays(-10), d.Key);
                d.DueDate.Should().Be(d.DocDate.AddDays(partners[d.PartnerCode].CreditDays!.Value));
            }
            else
            {
                d.DueDate.Should().BeNull();
            }
        }
    }

    [Theory]
    [InlineData("2026-05-01")]
    [InlineData("2026-10-14")]
    [InlineData("2026-08-31")]
    public void Opening_ages_follow_the_payment_habits(string today)
    {
        var plan = Generate(today: DateOnly.Parse(today));
        var partners = PartnersByCode(plan);
        var habits = SampleCatalog.CustomerProfiles.ToDictionary(p => p.Code, p => p.Habit);

        foreach (var d in plan.OpeningDebts.Where(d => d.Kind == OpeningDebtKind.Debt))
        {
            var age = plan.OpeningDate.DayNumber - d.DocDate.DayNumber;
            var creditDays = partners[d.PartnerCode].CreditDays!.Value;
            if (d.Side == DebtSide.Payable)
            {
                age.Should().BeInRange(10, creditDays, $"payable {d.Key} is paid near its due date, inside the window");
                continue;
            }
            switch (habits[d.PartnerCode])
            {
                case PaymentHabit.OnTime:
                    age.Should().BeInRange(10, creditDays, $"on-time {d.Key} is not overdue at opening");
                    break;
                case PaymentHabit.Late:
                    age.Should().BeInRange(10, creditDays + 30, $"late {d.Key} is collected inside the window");
                    break;
            }
        }
    }

    [Theory]
    [InlineData("2026-05-01")]
    [InlineData("2026-10-14")]
    [InlineData("2026-08-31")]
    public void Bad_debt_customer_has_one_item_61_to_90_days_overdue_at_today_and_one_older(string today)
    {
        var date = DateOnly.Parse(today);
        var badDebtCode = SampleCatalog.CustomerProfiles.Single(p => p.Habit == PaymentHabit.BadDebt).Code;

        var overdue = Generate(today: date).OpeningDebts
            .Where(d => d.PartnerCode == badDebtCode && d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt)
            .Select(d => date.DayNumber - d.DueDate!.Value.DayNumber)
            .ToList();

        overdue.Should().HaveCount(2);
        overdue.Should().ContainSingle(days => days >= 61 && days <= 90);
        overdue.Should().ContainSingle(days => days > 90);
    }

    [Fact]
    public void Opening_debt_sums_are_near_the_specified_totals()
    {
        var debts = Generate().OpeningDebts;

        debts.Where(d => d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt).Sum(d => d.Amount)
            .Should().BeInRange(300_000_000m, 400_000_000m);
        debts.Where(d => d.Side == DebtSide.Payable && d.Kind == OpeningDebtKind.Debt).Sum(d => d.Amount)
            .Should().BeInRange(150_000_000m, 250_000_000m);
    }

    [Fact]
    public void Opening_debt_owner_follows_the_customer()
    {
        var plan = Generate();
        var partners = PartnersByCode(plan);

        foreach (var d in plan.OpeningDebts)
        {
            d.OwnerIndex.Should().Be(d.Side == DebtSide.Receivable ? partners[d.PartnerCode].OwnerIndex : null);
        }
    }

    [Fact]
    public void With_one_owner_every_receivable_belongs_to_owner_zero()
    {
        var debts = Generate(ownerCount: 1).OpeningDebts;

        debts.Where(d => d.Side == DebtSide.Receivable).Should().OnlyContain(d => d.OwnerIndex == 0);
    }

    [Fact]
    public void Opening_balances_are_deterministic()
    {
        Generate().OpeningDebts.Should().BeEquivalentTo(Generate().OpeningDebts, o => o.WithStrictOrdering());
        Generate().OpeningStocks.Should().BeEquivalentTo(Generate().OpeningStocks, o => o.WithStrictOrdering());
    }
}
