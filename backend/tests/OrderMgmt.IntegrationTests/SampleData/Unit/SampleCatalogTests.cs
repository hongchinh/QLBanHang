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

    private static readonly (string Code, string Name, string Unit, decimal Price)[] ExpectedProducts =
    {
        ("DM-SP01", "Tấm xốp EPS 1000×2000×20mm, tỷ trọng 10", "TAM", 28000),
        ("DM-SP02", "Tấm xốp EPS 1000×2000×30mm, tỷ trọng 10", "TAM", 40000),
        ("DM-SP03", "Tấm xốp EPS 1000×2000×50mm, tỷ trọng 10", "TAM", 65000),
        ("DM-SP04", "Tấm xốp EPS 1000×2000×50mm, tỷ trọng 12", "TAM", 78000),
        ("DM-SP05", "Tấm xốp EPS 1000×2000×100mm, tỷ trọng 12", "TAM", 150000),
        ("DM-SP06", "Xốp EPS khối, tỷ trọng 10", "M3", 1350000),
        ("DM-SP07", "Tấm xốp XPS 600×1200×25mm", "TAM", 65000),
        ("DM-SP08", "Tấm xốp XPS 600×1200×30mm", "TAM", 78000),
        ("DM-SP09", "Tấm xốp XPS 600×1200×50mm", "TAM", 125000),
        ("DM-SP10", "Tấm xốp XPS 600×1200×100mm", "TAM", 245000),
        ("DM-SP11", "PE foam 1mm", "M2", 4500),
        ("DM-SP12", "PE foam 3mm", "M2", 9000),
        ("DM-SP13", "PE foam 5mm", "M2", 14000),
        ("DM-SP14", "PE foam 10mm", "M2", 26000),
        ("DM-SP15", "PE foam tráng bạc 5mm", "M2", 22000),
        ("DM-SP16", "Cao su non 1000×2000×10mm", "TAM", 120000),
        ("DM-SP17", "Cao su non 1000×2000×20mm", "TAM", 230000),
        ("DM-SP18", "Cao su non 1000×2000×30mm", "TAM", 340000),
        ("DM-SP19", "Cao su non tráng bạc 10mm", "TAM", 150000),
        ("DM-SP20", "Thùng xốp 5kg", "THUNG", 18000),
        ("DM-SP21", "Thùng xốp 10kg", "THUNG", 28000),
        ("DM-SP22", "Thùng xốp 20kg", "THUNG", 45000),
        ("DM-SP23", "Thùng xốp 30kg", "THUNG", 62000),
        ("DM-SP24", "Bông khoáng 40kg/m³ dày 50mm", "M2", 42000),
        ("DM-SP25", "Bông khoáng 60kg/m³ dày 50mm", "M2", 55000),
        ("DM-SP26", "Bông khoáng 100kg/m³ dày 50mm", "M2", 85000),
        ("DM-SP27", "Bông thủy tinh cuộn 12kg/m³ dày 50mm", "M2", 22000),
        ("DM-SP28", "Bông thủy tinh cuộn 24kg/m³ dày 50mm", "M2", 32000),
        ("DM-SP29", "Bông thủy tinh tấm 48kg/m³ dày 50mm", "M2", 75000),
        ("DM-SP30", "Vận chuyển nội thành", "CHUYEN", 350000),
    };

    [Fact]
    public void Dates_follow_the_calendar()
    {
        var plan = Generate();
        plan.OpeningDate.Should().Be(new DateOnly(2026, 7, 1));
        plan.LastDocumentDate.Should().Be(new DateOnly(2026, 10, 13));
    }

    [Fact]
    public void Products_match_the_spec_table_row_by_row()
    {
        var actual = Generate().Products.Select(p => (p.Code, p.Name, p.UnitCode, p.DefaultPrice)).ToList();
        actual.Should().Equal(ExpectedProducts);
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
    public void Dimensions_are_pinned_for_sample_products()
    {
        var products = Generate().Products;
        var sp01 = products.Single(p => p.Code == "DM-SP01");
        (sp01.Length, sp01.Width, sp01.Thickness, sp01.Density, sp01.Specification)
            .Should().Be((2000m, 1000m, 20m, 10m, "1000×2000×20mm"));
        var sp24 = products.Single(p => p.Code == "DM-SP24");
        (sp24.Length, sp24.Width, sp24.Thickness, sp24.Density, sp24.Specification)
            .Should().Be(((decimal?)null, (decimal?)null, (decimal?)50m, (decimal?)40m, "40kg/m³, dày 50mm"));
        var sp30 = products.Single(p => p.Code == "DM-SP30");
        (sp30.Length, sp30.Width, sp30.Thickness, sp30.Density, sp30.Specification)
            .Should().Be(((decimal?)null, (decimal?)null, (decimal?)null, (decimal?)null, (string?)null));
    }

    [Fact]
    public void Every_tracked_product_has_a_plan_with_a_real_primary_supplier()
    {
        var plan = Generate();
        var suppliers = plan.Partners.Where(p => p.IsSupplier).Select(p => p.Code).ToHashSet();
        var tracked = plan.Products.Where(p => p.TrackInventory).Select(p => p.Code).ToList();

        SampleCatalog.ProductPlans.Select(p => p.ProductCode).Should().Equal(tracked);
        SampleCatalog.ProductPlans.Should().OnlyContain(p => suppliers.Contains(p.PrimarySupplier));
        SampleCatalog.ProductPlans.Where(p => p.AltSupplier != null).Should().OnlyContain(p => suppliers.Contains(p.AltSupplier!));
        SampleCatalog.ProductPlans.Select(p => p.ProductCode).Should().NotContain("DM-SP30");
        SampleCatalog.ProductPlans.Should().OnlyContain(p => p.WeeklySales > 0 && p.WeeklySales == decimal.Truncate(p.WeeklySales));
    }

    [Fact]
    public void Alt_suppliers_include_dt01_for_boxes_and_ncc08_for_wool()
    {
        var plans = SampleCatalog.ProductPlans;
        plans.Where(p => p.AltSupplier == "DM-DT01").Select(p => p.ProductCode)
            .Should().Equal("DM-SP20", "DM-SP21", "DM-SP22", "DM-SP23");
        plans.Where(p => p.AltSupplier == "DM-NCC08").Select(p => p.ProductCode)
            .Should().Equal("DM-SP24", "DM-SP25", "DM-SP26", "DM-SP27", "DM-SP28", "DM-SP29");
    }

    [Fact]
    public void Warehouse_membership_is_26_in_k1_and_10_in_k2_covering_all_tracked()
    {
        var plans = SampleCatalog.ProductPlans;
        plans.Count(p => p.InK1).Should().Be(26);
        plans.Count(p => p.InK2).Should().Be(10);
        plans.Count(p => p.InK1 || p.InK2).Should().Be(29);
    }

    [Fact]
    public void Planned_sales_are_about_one_billion_a_month()
    {
        var prices = Generate().Products.ToDictionary(p => p.Code, p => p.DefaultPrice);
        var monthly = SampleCatalog.ProductPlans.Sum(p => p.WeeklySales * prices[p.ProductCode]) * 52m / 12m;
        monthly.Should().BeInRange(950_000_000m, 1_100_000_000m);
    }

    [Fact]
    public void Every_supplier_used_for_purchases_has_at_least_three_products()
    {
        var plans = SampleCatalog.ProductPlans;
        var supplierCodes = plans.Select(p => p.PrimarySupplier)
            .Concat(plans.Where(p => p.AltSupplier != null).Select(p => p.AltSupplier!))
            .Distinct();

        foreach (var supplier in supplierCodes)
        {
            plans.Count(p => p.PrimarySupplier == supplier || p.AltSupplier == supplier)
                .Should().BeGreaterThanOrEqualTo(3, $"supplier {supplier} must be able to fill a 3-line purchase");
        }
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
    public void Customer_profiles_follow_the_role_rules()
    {
        var plan = Generate(3);
        var profiles = SampleCatalog.CustomerProfiles;
        var customers = plan.Partners.Where(p => p.IsCustomer && !p.IsSupplier).Select(p => p.Code).ToList();
        profiles.Where(p => p.Code.StartsWith("DM-KH")).Select(p => p.Code).Should().Equal(customers);
        profiles.Select(p => p.Code).Should().OnlyHaveUniqueItems();

        var late = profiles.Where(p => p.Habit == PaymentHabit.Late).ToList();
        late.Should().HaveCount(2);
        late.Select(p => p.Group).Should().BeEquivalentTo(new[] { CustomerGroup.Company, CustomerGroup.Agent });
        profiles.Where(p => p.Habit == PaymentHabit.BadDebt).Should().ContainSingle().Which.Group.Should().Be(CustomerGroup.Project);
        profiles.Where(p => p.HasPrepayment).Should().ContainSingle().Which.Group.Should().Be(CustomerGroup.Retail);

        var limited = profiles.Where(p => p.CreditLimit != null).ToList();
        limited.Should().HaveCount(2).And.OnlyContain(p => p.Group == CustomerGroup.Agent && p.CreditLimit == 300_000_000m);
        limited.Select(p => p.Code).Should().NotIntersectWith(late.Select(p => p.Code));
        plan.CreditLimits.Select(l => (l.PartnerCode, l.Amount))
            .Should().Equal(limited.Select(p => (p.Code, p.CreditLimit!.Value)));

        var kh = profiles.Where(p => p.Code.StartsWith("DM-KH")).ToList();
        kh.Count(p => p.Tier == SalesTier.Large).Should().Be(5);
        kh.Count(p => p.Tier == SalesTier.Medium).Should().Be(10);
        kh.Count(p => p.Tier == SalesTier.Small).Should().Be(5);

        var owners = plan.Partners.ToDictionary(p => p.Code, p => p.OwnerIndex);
        kh.Where(p => p.Tier == SalesTier.Large).Select(p => owners[p.Code]).Distinct()
            .Should().BeEquivalentTo(new int?[] { 0, 1, 2 });

        profiles.Where(p => p.Code.StartsWith("DM-DT")).Should()
            .HaveCount(2).And.OnlyContain(p => p.Habit == PaymentHabit.OnTime && p.Tier == SalesTier.Medium);
    }
}
