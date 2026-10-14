using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class SampleDataRunnerSetupTests : CashDebtTestBase
{
    public SampleDataRunnerSetupTests(PostgresFixture pg) : base(pg) { }

    private Task<SampleDataRunResult> RunPlanAsync(Guid branchId, SampleDataPlan plan) =>
        TinyPlans.RunAsync(_factory, branchId, plan);

    [Fact]
    public async Task Setup_creates_catalog_warehouses_funds_and_opening_balances()
    {
        var branchId = await CreateBranchAsync("RS01");
        var adminId = await TinyPlans.AdminIdAsync(_factory);
        // The clone may already hold SALES users, so the expected owner comes from the S5 query.
        var expectedOwner = (await TinyPlans.SalesOwnersAsync(_factory)).Cast<Guid?>().FirstOrDefault() ?? adminId;

        var result = await RunPlanAsync(branchId, TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeTrue(result.Error);
        result.StepsExecuted.Should().Be(0);
        result.StepsSkipped.Should().Be(0);
        result.Error.Should().BeNull();

        var branch = await InDbAsync(db => db.Branches.AsNoTracking().SingleAsync(b => b.Id == branchId));
        branch.SampleDataStatus.Should().Be(SampleDataStatus.Ready);
        branch.SampleDataProgress.Should().Be(100);
        branch.SampleDataError.Should().BeNull();
        branch.OpeningDate.Should().Be(TinyPlans.OpeningDate);

        // Catalog.
        var group = await InDbAsync(db => db.ProductGroups.AsNoTracking().SingleAsync(g => g.Code == "DM"));
        group.Name.Should().Be("Dữ liệu mẫu");
        var products = await InDbAsync(db => db.Products.AsNoTracking()
            .Where(p => p.Code.StartsWith("DM-")).ToDictionaryAsync(p => p.Code));
        products.Keys.Should().BeEquivalentTo("DM-SP01", "DM-SP30");
        foreach (var product in products.Values)
        {
            product.IsSample.Should().BeTrue(product.Code);
            product.ProductGroupId.Should().Be(group.Id, product.Code);
            product.DefaultTaxRate.Should().Be(10m, product.Code);
        }
        products["DM-SP01"].TrackInventory.Should().BeTrue();
        products["DM-SP30"].TrackInventory.Should().BeFalse();

        var partners = await InDbAsync(db => db.Customers.AsNoTracking()
            .Where(c => c.Code.StartsWith("DM-")).ToDictionaryAsync(c => c.Code));
        partners.Keys.Should().BeEquivalentTo("DM-KH01", "DM-NCC01", "DM-DT01");
        partners.Values.Should().OnlyContain(p => p.IsSample);
        (partners["DM-KH01"].IsCustomer, partners["DM-KH01"].IsSupplier).Should().Be((true, false));
        (partners["DM-NCC01"].IsCustomer, partners["DM-NCC01"].IsSupplier).Should().Be((false, true));
        (partners["DM-DT01"].IsCustomer, partners["DM-DT01"].IsSupplier).Should().Be((true, true));
        partners["DM-KH01"].Group.Should().Be(CustomerGroup.Agent);
        partners["DM-KH01"].CreditDays.Should().Be(30);

        // Warehouse and funds; setup items act as the requesting user.
        var warehouse = await InDbAsync(db => db.Warehouses.AsNoTracking().SingleAsync(w => w.Code == "RS01-K1"));
        warehouse.BranchId.Should().Be(branchId);
        warehouse.CreatedBy.Should().Be(adminId);

        var funds = await InDbAsync(db => db.Funds.AsNoTracking()
            .Where(f => f.BranchId == branchId).ToDictionaryAsync(f => f.Code));
        funds.Keys.Should().BeEquivalentTo("QTM", "VCB");
        funds["QTM"].OpeningAmount.Should().Be(10_000_000m);
        funds["VCB"].Kind.Should().Be(FundKind.Bank);
        funds["VCB"].IsDefault.Should().BeTrue();
        funds["VCB"].OpeningAmount.Should().Be(100_000_000m);

        // Opening balances.
        var stock = await InDbAsync(db => db.OpeningStocks.AsNoTracking()
            .Where(o => o.BranchId == branchId).ToListAsync());
        stock.Should().ContainSingle();
        stock[0].WarehouseId.Should().Be(warehouse.Id);
        stock[0].ProductId.Should().Be(products["DM-SP01"].Id);
        stock[0].Quantity.Should().Be(100m);
        stock[0].Amount.Should().Be(5_000_000m);

        var debts = await InDbAsync(db => db.OpeningDebts.AsNoTracking()
            .Where(o => o.BranchId == branchId).ToDictionaryAsync(o => o.DocNo));
        debts.Keys.Should().BeEquivalentTo("HD-DK-01", "HDM-DK-01");
        var od01 = debts["HD-DK-01"];
        od01.PartnerId.Should().Be(partners["DM-KH01"].Id);
        od01.Side.Should().Be(DebtSide.Receivable);
        od01.Amount.Should().Be(3_000_000m);
        od01.DocDate.Should().Be(TinyPlans.OpeningDate.AddDays(-20));
        od01.DueDate.Should().Be(TinyPlans.OpeningDate.AddDays(10));
        od01.OwnerUserId.Should().Be(expectedOwner);
        var od02 = debts["HDM-DK-01"];
        od02.PartnerId.Should().Be(partners["DM-NCC01"].Id);
        od02.Side.Should().Be(DebtSide.Payable);
        od02.Amount.Should().Be(2_000_000m);
        od02.OwnerUserId.Should().Be(adminId);

        var kh01Id = partners["DM-KH01"].Id;
        var limit = await InDbAsync(db => db.CreditLimits.AsNoTracking()
            .SingleAsync(l => l.BranchId == branchId && l.CustomerId == kh01Id));
        limit.Amount.Should().Be(50_000_000m);

        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }
}
