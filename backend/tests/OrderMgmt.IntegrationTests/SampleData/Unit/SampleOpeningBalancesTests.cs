using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleOpeningBalancesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static SampleDataPlan Generate(int ownerCount = 3) =>
        new SampleDataGenerator().Generate(new SampleDataGeneratorInput("DEMO01", Today, ownerCount));

    private static Dictionary<string, SamplePartnerSpec> PartnersByCode(SampleDataPlan plan) =>
        plan.Partners.ToDictionary(p => p.Code);

    [Theory]
    [InlineData("K1", 26)]
    [InlineData("K2", 10)]
    public void OpeningStock_HasTheTrackedProductsOfTheWarehouse(string key, int expectedLines)
    {
        var plan = Generate();

        var stock = plan.OpeningStocks.Single(s => s.WarehouseKey == key);

        stock.Lines.Should().HaveCount(expectedLines);
        stock.Lines.Select(l => l.ProductCode).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void OpeningStock_LinesAreTrackedWholeQuantitiesPricedAtCost()
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
    public void OpeningStock_K2OnlyProductsHoldAtLeastThreeWeeksOfSales()
    {
        var k2 = Generate().OpeningStocks.Single(s => s.WarehouseKey == "K2").Lines.ToDictionary(l => l.ProductCode);
        var k2Only = SampleCatalog.ProductPlans.Where(p => p.InK2 && !p.InK1).ToList();

        k2Only.Should().NotBeEmpty();
        foreach (var plan in k2Only)
        {
            k2[plan.ProductCode].Quantity.Should().BeGreaterThanOrEqualTo(3 * plan.WeeklySales);
        }
    }

    [Fact]
    public void OpeningDebts_HaveSixteenUniqueKeysInOrder()
    {
        var debts = Generate().OpeningDebts;

        debts.Should().HaveCount(16);
        debts.Select(d => d.Key).Should().Equal(Enumerable.Range(1, 16).Select(i => $"OD-{i:00}"));
        debts.Select(d => d.DocNo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void OpeningDebts_HaveTheSpecifiedShape()
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
    public void OpeningDebts_DatesAndAmountsAreConsistent()
    {
        var plan = Generate();
        var partners = PartnersByCode(plan);

        foreach (var d in plan.OpeningDebts)
        {
            d.DocDate.Should().BeOnOrBefore(plan.OpeningDate);
            d.Amount.Should().BeGreaterThan(0);
            if (d.Kind == OpeningDebtKind.Debt)
            {
                d.DueDate.Should().Be(d.DocDate.AddDays(partners[d.PartnerCode].CreditDays!.Value));
                d.DueDate.Should().BeOnOrAfter(d.DocDate);
            }
            else
            {
                d.DueDate.Should().BeNull();
            }
        }
    }

    [Fact]
    public void OpeningDebts_SumsAreNearTheSpecifiedTotals()
    {
        var debts = Generate().OpeningDebts;

        debts.Where(d => d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt).Sum(d => d.Amount)
            .Should().BeInRange(300_000_000m, 400_000_000m);
        debts.Where(d => d.Side == DebtSide.Payable && d.Kind == OpeningDebtKind.Debt).Sum(d => d.Amount)
            .Should().BeInRange(150_000_000m, 250_000_000m);
    }

    [Fact]
    public void OpeningDebts_OwnerIndexFollowsTheCustomer()
    {
        var plan = Generate();
        var partners = PartnersByCode(plan);

        foreach (var d in plan.OpeningDebts)
        {
            d.OwnerIndex.Should().Be(d.Side == DebtSide.Receivable ? partners[d.PartnerCode].OwnerIndex : null);
        }
    }

    [Fact]
    public void OpeningDebts_WithOneOwnerEveryReceivableBelongsToOwnerZero()
    {
        var debts = Generate(ownerCount: 1).OpeningDebts;

        debts.Where(d => d.Side == DebtSide.Receivable).Should().OnlyContain(d => d.OwnerIndex == 0);
    }

    [Fact]
    public void OpeningDebts_BadDebtCustomerHasAReceivableDebt()
    {
        var badDebtCode = SampleCatalog.CustomerProfiles.Single(p => p.Habit == PaymentHabit.BadDebt).Code;

        Generate().OpeningDebts.Should().Contain(d =>
            d.PartnerCode == badDebtCode && d.Side == DebtSide.Receivable && d.Kind == OpeningDebtKind.Debt);
    }

    [Fact]
    public void OpeningBalances_AreDeterministic()
    {
        Generate().OpeningDebts.Should().BeEquivalentTo(Generate().OpeningDebts, o => o.WithStrictOrdering());
        Generate().OpeningStocks.Should().BeEquivalentTo(Generate().OpeningStocks, o => o.WithStrictOrdering());
    }
}
