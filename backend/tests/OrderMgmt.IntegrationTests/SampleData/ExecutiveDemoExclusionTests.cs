using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.ExecutiveDashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

/// Plan S15: company-wide executive figures skip demo branches; a demo branch named explicitly can still be viewed.
[Collection(nameof(PostgresCollection))]
public class ExecutiveDemoExclusionTests : ExecutiveDashboardTestBase
{
    public ExecutiveDemoExclusionTests(PostgresFixture pg) : base(pg) { }

    private Task MarkDemoAsync(Guid branchId) =>
        InDbAsync(db => db.Branches.Where(b => b.Id == branchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDemo, true)));

    [Fact]
    public async Task Branch_comparison_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = CloneAdminClient();
        var before = await GetExecAsync<List<BranchComparisonRowDto>>(client, $"branches?{October}");
        before.Should().Contain(r => r.BranchId == ids.BranchBId);

        await MarkDemoAsync(ids.BranchBId);

        var after = await GetExecAsync<List<BranchComparisonRowDto>>(client, $"branches?{October}");
        after.Should().NotContain(r => r.BranchId == ids.BranchBId);
        after.Select(r => r.BranchId).Should().Equal(before.Select(r => r.BranchId).Where(id => id != ids.BranchBId));
        after.Single(r => r.BranchId == ids.MainBranchId).Should()
            .BeEquivalentTo(before.Single(r => r.BranchId == ids.MainBranchId));
    }

    [Fact]
    public async Task Company_sales_summary_skips_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = CloneAdminClient();
        var main = await GetExecAsync<SalesSummaryDto>(client, $"sales/summary?{OctoberRange}&branchId={ids.MainBranchId}");
        var before = await GetExecAsync<SalesSummaryDto>(client, $"sales/summary?{OctoberRange}");
        before.Revenue.Should().NotBe(main.Revenue);

        await MarkDemoAsync(ids.BranchBId);

        var after = await GetExecAsync<SalesSummaryDto>(client, $"sales/summary?{OctoberRange}");
        after.Should().BeEquivalentTo(main);
    }

    [Fact]
    public async Task Company_debt_and_inventory_skip_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // The dataset has no tracked stock: branch B receives 7 × 100 so the inventory figures differ too.
        var warehouseB = await InDbAsync(db => db.Warehouses.IgnoreQueryFilters()
            .Where(w => w.BranchId == ids.BranchBId).Select(w => w.Id).FirstAsync());
        var product = await CreateInventoryProductAsync("EX" + Guid.NewGuid().ToString("N")[..8]);
        var clientB = CloneAdminClient();
        UseBranch(clientB, ids.BranchBId);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-05 09:00",
            LineRequest(product, 7, 100m));
        request.WarehouseId = warehouseB;
        request.PartnerId = ids.Ncc1Id;
        var (status, _, error) = await PostVoucherAsync(clientB, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        var client = CloneAdminClient();
        var debtQuery = $"debt/summary?{October}";
        var inventoryQuery = $"inventory/summary?{OctoberRange}";
        var branchMain = $"&branchId={ids.MainBranchId}";

        var debtMain = await GetExecAsync<DebtSummaryDto>(client, debtQuery + branchMain);
        var debtBefore = await GetExecAsync<DebtSummaryDto>(client, debtQuery);
        debtBefore.Receivable.Balance.Should().NotBe(debtMain.Receivable.Balance);
        var inventoryMain = await GetExecAsync<InventorySummaryDto>(client, inventoryQuery + branchMain);
        var inventoryBefore = await GetExecAsync<InventorySummaryDto>(client, inventoryQuery);
        inventoryBefore.StockValue.Should().NotBe(inventoryMain.StockValue);

        await MarkDemoAsync(ids.BranchBId);

        // Both are checked: a debt failure must not hide the inventory result.
        using var scope = new AssertionScope();
        (await GetExecAsync<DebtSummaryDto>(client, debtQuery)).Should().BeEquivalentTo(debtMain);
        (await GetExecAsync<InventorySummaryDto>(client, inventoryQuery)).Should().BeEquivalentTo(inventoryMain);
    }

    /// A guard on the routes that read the database directly: they take their branches from the resolver too.
    [Fact]
    public async Task Recent_documents_and_cash_funds_skip_demo_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = CloneAdminClient();
        var recentBefore = await GetExecAsync<List<RecentDocumentDto>>(client, "recent?limit=50");
        recentBefore.Should().Contain(r => r.Id == ids.BranchBSaleId);
        var cashBefore = await GetExecAsync<CashSummaryDto>(client, $"cash/summary?{October}");
        cashBefore.Funds.Should().Contain(f => f.BranchId == ids.BranchBId);

        await MarkDemoAsync(ids.BranchBId);

        using var scope = new AssertionScope();
        (await GetExecAsync<List<RecentDocumentDto>>(client, "recent?limit=50"))
            .Should().NotContain(r => r.Id == ids.BranchBSaleId);
        (await GetExecAsync<CashSummaryDto>(client, $"cash/summary?{October}"))
            .Funds.Should().NotContain(f => f.BranchId == ids.BranchBId);
    }

    [Fact]
    public async Task Demo_branch_can_still_be_viewed_on_its_own()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = CloneAdminClient();
        var query = $"sales/summary?{OctoberRange}&branchId={ids.BranchBId}";
        var before = await GetExecAsync<SalesSummaryDto>(client, query);
        before.Revenue.Should().Be(9_999m);

        await MarkDemoAsync(ids.BranchBId);

        var response = await GetExecRawAsync(client, query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<SalesSummaryDto>(response)).Should().BeEquivalentTo(before);
    }
}
