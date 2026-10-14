using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Organization.SampleData.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
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
        // A SALES user exists, so OD-01 (OwnerIndex 0) is not owned by the requesting admin. The clone may hold other
        // SALES users, so the expected owner comes from the S5 query.
        await CreateTestUserAsync("sd_owner_a", "Pass@123", RoleCodes.Sales);
        var owners = await TinyPlans.SalesOwnersAsync(_factory);
        owners.Should().NotBeEmpty();

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
        od01.OwnerUserId.Should().Be(owners[0]);
        od01.OwnerUserId.Should().NotBe(adminId);
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

    [Fact]
    public async Task Second_branch_reuses_the_sample_catalog()
    {
        var first = await RunPlanAsync(await CreateBranchAsync("RS02"), TinyPlans.Setup(TinyPlans.OpeningDate));
        var second = await RunPlanAsync(await CreateBranchAsync("RS03"), TinyPlans.Setup(TinyPlans.OpeningDate));

        first.Succeeded.Should().BeTrue(first.Error);
        second.Succeeded.Should().BeTrue(second.Error);
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync(p => p.Code == "DM-SP01"))).Should().Be(1);
        (await InDbAsync(db => db.Customers.IgnoreQueryFilters().CountAsync(c => c.Code == "DM-KH01"))).Should().Be(1);
        (await InDbAsync(db => db.ProductGroups.IgnoreQueryFilters().CountAsync(g => g.Code == "DM"))).Should().Be(1);
    }

    [Fact]
    public async Task Soft_deleted_sample_rows_are_restored()
    {
        (await RunPlanAsync(await CreateBranchAsync("RS04"), TinyPlans.Setup(TinyPlans.OpeningDate))).Succeeded.Should().BeTrue();
        // The group goes through the real delete, which also clears the group of its products.
        var groupId = await InDbAsync(db => db.ProductGroups.Where(g => g.Code == "DM").Select(g => g.Id).SingleAsync());
        var deleted = await _client.DeleteAsync($"/api/product-groups/{groupId}");
        deleted.IsSuccessStatusCode.Should().BeTrue(await deleted.Content.ReadAsStringAsync());
        await InDbAsync(async db =>
        {
            var deletedAt = DateTimeOffset.UtcNow;
            var product = await db.Products.SingleAsync(p => p.Code == "DM-SP01");
            var partner = await db.Customers.SingleAsync(c => c.Code == "DM-KH01");
            foreach (var row in new BaseEntity[] { product, partner })
            {
                row.IsDeleted = true;
                row.DeletedAt = deletedAt;
                row.DeletedBy = Guid.NewGuid();
            }
            await db.SaveChangesAsync();
        });
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().AnyAsync(p => p.ProductGroupId == groupId)))
            .Should().BeFalse("the group delete cleared the products' group");

        var result = await RunPlanAsync(await CreateBranchAsync("RS05"), TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeTrue(result.Error);
        var group = await InDbAsync(db => db.ProductGroups.IgnoreQueryFilters().AsNoTracking().SingleAsync(g => g.Code == "DM"));
        var product2 = await InDbAsync(db => db.Products.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Code == "DM-SP01"));
        var partner2 = await InDbAsync(db => db.Customers.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Code == "DM-KH01"));
        group.Id.Should().Be(groupId);
        foreach (var row in new BaseEntity[] { group, product2, partner2 })
        {
            row.IsDeleted.Should().BeFalse();
            row.DeletedAt.Should().BeNull();
            row.DeletedBy.Should().BeNull();
        }
        product2.ProductGroupId.Should().Be(groupId, "a reused sample product gets its group back");
        (await InDbAsync(db => db.Products.Where(p => p.Code.StartsWith("DM-")).AllAsync(p => p.ProductGroupId == groupId)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Reused_sample_rows_are_usable_again()
    {
        (await RunPlanAsync(await CreateBranchAsync("RS07"), TinyPlans.Setup(TinyPlans.OpeningDate))).Succeeded.Should().BeTrue();
        // A user stops using the sample rows and drops the supplier role of the dual partner.
        await InDbAsync(async db =>
        {
            (await db.ProductGroups.SingleAsync(g => g.Code == "DM")).IsActive = false;
            (await db.Products.SingleAsync(p => p.Code == "DM-SP01")).Status = ProductStatus.Inactive;
            (await db.Customers.SingleAsync(c => c.Code == "DM-KH01")).Status = CustomerStatus.Inactive;
            (await db.Customers.SingleAsync(c => c.Code == "DM-DT01")).IsSupplier = false;
            await db.SaveChangesAsync();
        });

        var result = await RunPlanAsync(await CreateBranchAsync("RS08"), TinyPlans.Setup(TinyPlans.OpeningDate));

        result.Succeeded.Should().BeTrue(result.Error);
        (await InDbAsync(db => db.ProductGroups.SingleAsync(g => g.Code == "DM"))).IsActive.Should().BeTrue();
        (await InDbAsync(db => db.Products.SingleAsync(p => p.Code == "DM-SP01"))).Status.Should().Be(ProductStatus.Active);
        (await InDbAsync(db => db.Customers.SingleAsync(c => c.Code == "DM-KH01"))).Status.Should().Be(CustomerStatus.Active);
        var dual = await InDbAsync(db => db.Customers.SingleAsync(c => c.Code == "DM-DT01"));
        (dual.IsCustomer, dual.IsSupplier).Should().Be((true, true));
    }

    [Fact]
    public async Task Branch_that_is_not_demo_is_refused()
    {
        var branchId = await CreateBranchAsync("RS09");

        var result = await TinyPlans.RunAsync(_factory, branchId, TinyPlans.Setup(TinyPlans.OpeningDate), asDemo: false);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("KE-HOACH: ").And.Contain("chi nhánh demo");
        var branch = await InDbAsync(db => db.Branches.AsNoTracking().SingleAsync(b => b.Id == branchId));
        branch.SampleDataStatus.Should().Be(SampleDataStatus.None, "a real branch's status is never touched");
        branch.SampleDataError.Should().BeNull();
        branch.OpeningDate.Should().BeNull();
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.BranchId == branchId))).Should().BeFalse();
        (await InDbAsync(db => db.Products.IgnoreQueryFilters().AnyAsync(p => p.Code.StartsWith("DM-")))).Should().BeFalse();
    }
}
