using FluentAssertions;
using OrderMgmt.Application.Organization.SampleData.Generation;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData.Unit;

public class SampleCatalogTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static SampleDataPlan Generate(int ownerCount = 3) =>
        new SampleDataGenerator().Generate(new SampleDataGeneratorInput("DEMO01", Today, ownerCount));

    private static readonly decimal[] Prices =
    {
        28000, 40000, 65000, 78000, 150000, 1350000, 65000, 78000, 125000, 245000,
        4500, 9000, 14000, 26000, 22000, 120000, 230000, 340000, 150000,
        18000, 28000, 45000, 62000, 42000, 55000, 85000, 22000, 32000, 75000, 350000,
    };

    [Fact]
    public void Dates_follow_the_calendar()
    {
        var plan = Generate();
        plan.OpeningDate.Should().Be(new DateOnly(2026, 7, 1));
        plan.LastDocumentDate.Should().Be(new DateOnly(2026, 10, 13));
    }

    [Fact]
    public void Products_are_thirty_in_order_with_spec_prices()
    {
        var products = Generate().Products;
        products.Select(p => p.Code).Should().Equal(Enumerable.Range(1, 30).Select(i => $"DM-SP{i:00}"));
        products.Select(p => p.DefaultPrice).Should().Equal(Prices);
        products.Select(p => p.UnitCode).Distinct().Should().BeSubsetOf(new[] { "TAM", "M2", "M3", "THUNG", "CHUYEN" });
    }

    [Theory]
    [InlineData("DM-SP01", "Tấm xốp EPS 1000×2000×20mm, tỷ trọng 10", "TAM", 28000)]
    [InlineData("DM-SP06", "Xốp EPS khối, tỷ trọng 10", "M3", 1350000)]
    [InlineData("DM-SP11", "PE foam 1mm", "M2", 4500)]
    [InlineData("DM-SP20", "Thùng xốp 5kg", "THUNG", 18000)]
    [InlineData("DM-SP30", "Vận chuyển nội thành", "CHUYEN", 350000)]
    public void Product_rows_match_the_spec(string code, string name, string unit, decimal price)
    {
        var p = Generate().Products.Single(x => x.Code == code);
        p.Name.Should().Be(name);
        p.UnitCode.Should().Be(unit);
        p.DefaultPrice.Should().Be(price);
    }

    [Fact]
    public void Only_transport_is_untracked_and_cost_is_78_percent_rounded_to_hundreds()
    {
        var products = Generate().Products;
        products.Where(p => !p.TrackInventory).Select(p => p.Code).Should().Equal("DM-SP30");
        foreach (var p in products)
        {
            p.CostPrice.Should().Be(Math.Round(p.DefaultPrice * 0.78m / 100m, MidpointRounding.AwayFromZero) * 100m);
        }
        products.Single(p => p.Code == "DM-SP01").CostPrice.Should().Be(21800m);
    }

    [Fact]
    public void Partners_split_into_customers_suppliers_and_both()
    {
        var partners = Generate().Partners;
        partners.Should().HaveCount(30);
        partners.Select(p => p.Code).Should().OnlyHaveUniqueItems();
        partners.Should().OnlyContain(p => p.Name.EndsWith(" (mẫu)"));

        var customers = partners.Where(p => p.IsCustomer && !p.IsSupplier).ToList();
        customers.Select(p => p.Code).Should().Equal(Enumerable.Range(1, 20).Select(i => $"DM-KH{i:00}"));
        customers.Count(p => p.Group == CustomerGroup.Company).Should().Be(6);
        customers.Where(p => p.Group == CustomerGroup.Company).Should().OnlyContain(p => p.CreditDays == 30);
        customers.Where(p => p.Group == CustomerGroup.Agent).Select(p => p.CreditDays)
            .Should().BeEquivalentTo(new int?[] { 15, 30, 30, 45, 45 });
        customers.Where(p => p.Group == CustomerGroup.Retail).Should().HaveCount(5).And.OnlyContain(p => p.CreditDays == 0);
        customers.Where(p => p.Group == CustomerGroup.Project).Should().HaveCount(4).And.OnlyContain(p => p.CreditDays == 45);

        var suppliers = partners.Where(p => p.IsSupplier && !p.IsCustomer).ToList();
        suppliers.Select(p => p.Code).Should().Equal(Enumerable.Range(1, 8).Select(i => $"DM-NCC{i:00}"));
        suppliers.Count(p => p.CreditDays == 15).Should().Be(1);
        suppliers.Count(p => p.CreditDays == 45).Should().Be(1);
        suppliers.Count(p => p.CreditDays == 30).Should().Be(6);

        var both = partners.Where(p => p.IsCustomer && p.IsSupplier).ToList();
        both.Select(p => p.Code).Should().Equal("DM-DT01", "DM-DT02");
        both.Should().OnlyContain(p => p.CreditDays == 30);
    }

    [Fact]
    public void Owner_indexes_rotate_over_customers_then_both_roles()
    {
        var partners = Generate(3).Partners;
        var owned = partners.Where(p => p.IsCustomer).ToList();
        owned.Should().HaveCount(22);
        owned.Select(p => p.OwnerIndex).Should().Equal(Enumerable.Range(0, 22).Select(i => (int?)(i % 3)));
        partners.Where(p => !p.IsCustomer).Should().OnlyContain(p => p.OwnerIndex == null);
    }

    [Fact]
    public void Single_owner_means_index_zero()
    {
        Generate(1).Partners.Where(p => p.OwnerIndex != null).Should().OnlyContain(p => p.OwnerIndex == 0);
    }

    [Fact]
    public void Zero_owners_is_rejected()
    {
        var act = () => Generate(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Warehouses_and_funds_match_the_rules()
    {
        var plan = Generate();
        plan.Warehouses.Select(w => w.Key).Should().Equal("K1", "K2");
        plan.Funds.Select(f => (f.Code, f.Kind, f.BankName, f.AccountNumber, f.OpeningAmount)).Should().Equal(
            ("QTM", FundKind.Cash, (string?)null, (string?)null, 30_000_000m),
            ("VCB", FundKind.Bank, "Vietcombank", "9999000001", 450_000_000m),
            ("TCB", FundKind.Bank, "Techcombank", "9999000002", 150_000_000m));
    }

    [Fact]
    public void Two_agents_carry_credit_limits()
    {
        var plan = Generate();
        plan.CreditLimits.Should().HaveCount(2).And.OnlyContain(l => l.Amount == 300_000_000m);
        foreach (var limit in plan.CreditLimits)
        {
            plan.Partners.Single(p => p.Code == limit.PartnerCode).Group.Should().Be(CustomerGroup.Agent);
        }
        plan.CreditLimits.Select(l => l.PartnerCode).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Opening_data_and_steps_stay_empty()
    {
        var plan = Generate();
        plan.Steps.Should().BeEmpty();
        plan.OpeningStocks.Should().BeEmpty();
        plan.OpeningDebts.Should().BeEmpty();
    }
}
