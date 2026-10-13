using FluentAssertions;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Cost, gross profit, margin and the provisional flag of sales/summary (Phase 01, Task 1.5).
[Collection(nameof(PostgresCollection))]
public class SalesCostTests : ExecutiveDashboardTestBase
{
    public SalesCostTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Cost_and_gross_profit_of_a_tracked_sale()
    {
        var (branch, _, _) = await SeedTrackedSaleAsync();

        var summary = await GetSummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        summary.Revenue.Should().Be(600m);
        summary.Cost.Should().Be(400m);
        summary.GrossProfit.Should().Be(200m);
        summary.MarginPct.Should().Be(33.33m);
        summary.CanViewCost.Should().BeTrue();
    }

    [Fact]
    public async Task Return_reduces_cost_by_its_inbound_value()
    {
        var (branch, product, sale) = await SeedTrackedSaleAsync();
        var returned = await PostLineAsync(branch, StockDirection.In, "NTL", "2026-10-06 09:00", branch.CustomerId,
            product, 1, 150);
        var soldCost = (await LedgerOfAsync(sale.Id))
            .Where(e => e.SourceType == LedgerSourceType.StockOut).Sum(e => e.CostAmount ?? 0m);
        var returnedValue = (await LedgerOfAsync(returned.Id))
            .Where(e => e.SourceType == LedgerSourceType.StockIn).Sum(e => e.InValue);

        var summary = await GetSummaryAsync(branch.Client, $"{October}&branchId={branch.BranchId}");

        returnedValue.Should().BePositive();
        summary.Revenue.Should().Be(450m);
        summary.Cost.Should().Be(soldCost - returnedValue);
    }

    [Fact]
    public async Task Untracked_line_has_no_cost()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var summary = await GetSummaryAsync(MainClient(ids), $"{October}&branchId={ids.MainBranchId}");

        summary.Cost.Should().Be(0m);
        summary.GrossProfit.Should().Be(2_500m);
        summary.MarginPct.Should().Be(100m);
    }

    [Fact]
    public async Task Previous_period_has_its_own_cost_and_margin()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("COST-02");
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-01 09:00", branch.SupplierId, product, 20, 100);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, product, 4, 150);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-07 09:00", branch.CustomerId, product, 2, 200);

        // The period 10-06 .. 10-08 holds the second sale; the 3 days before it (10-03 .. 10-05) hold the first.
        var summary = await GetSummaryAsync(branch.Client, $"from=2026-10-06&to=2026-10-08&branchId={branch.BranchId}");

        summary.Revenue.Should().Be(400m);
        summary.PrevRevenue.Should().Be(600m);
        summary.RevenueDeltaPct.Should().Be(-33.33m);
        summary.Cost.Should().Be(200m);
        summary.GrossProfit.Should().Be(200m);
        summary.MarginPct.Should().Be(50m);
        summary.PrevMarginPct.Should().Be(33.33m);
    }

    [Fact]
    public async Task Cost_is_hidden_without_view_cost()
    {
        var (branch, _, _) = await SeedTrackedSaleAsync();
        var user = await CreateClientWithPermissionsAsync("no_cost_user", branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);

        var summary = await GetSummaryAsync(user, $"{October}&branchId={branch.BranchId}");

        summary.Revenue.Should().Be(600m);
        summary.CanViewCost.Should().BeFalse();
        summary.Cost.Should().BeNull();
        summary.GrossProfit.Should().BeNull();
        summary.MarginPct.Should().BeNull();
        summary.PrevMarginPct.Should().BeNull();
    }

    [Fact]
    public async Task Provisional_follows_the_costing_period()
    {
        var client = CloneAdminClient();

        (await GetSummaryAsync(client, "from=2020-01-01&to=2020-01-31")).IsProvisional.Should().BeFalse();
        (await GetSummaryAsync(client, "from=2099-12-01&to=2099-12-31")).IsProvisional.Should().BeTrue();
    }
}
