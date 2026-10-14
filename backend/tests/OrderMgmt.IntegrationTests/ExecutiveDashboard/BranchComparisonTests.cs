using System.Net;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// branches: one row per branch with its revenue and margin of the period, its receivable at the balance date and the
/// value of its stock now. Each column shows only what the user may see.
[Collection(nameof(PostgresCollection))]
public class BranchComparisonTests : ExecutiveDashboardTestBase
{
    public BranchComparisonTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task One_row_per_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var seeded = await InDbAsync(db => db.Branches.Where(b => b.Code == "CN01")
            .Select(b => new { b.Id, b.Name }).SingleAsync());
        var suffix = ids.Kh1Code[3..];

        var rows = await GetBranchesAsync(CloneAdminClient(), October);

        // CN01, then RB… (branch B), then RD… (the main branch).
        rows.Select(r => (r.BranchId, r.BranchCode)).Should().Equal(
            (seeded.Id, "CN01"), (ids.BranchBId, "RB" + suffix), (ids.MainBranchId, "RD" + suffix));
        rows.Select(r => r.BranchName).Should().Equal(seeded.Name, $"Chi nhánh RB{suffix}", $"Chi nhánh RD{suffix}");

        var main = rows.Single(r => r.BranchId == ids.MainBranchId);
        (main.Revenue, main.MarginPct, main.Receivable, main.StockValue)
            .Should().Be(((decimal?)2_500m, (decimal?)100m, (decimal?)1_500m, (decimal?)0m));
        var branchB = rows.Single(r => r.BranchId == ids.BranchBId);
        (branchB.Revenue, branchB.MarginPct, branchB.Receivable, branchB.StockValue)
            .Should().Be(((decimal?)9_999m, (decimal?)100m, (decimal?)9_999m, (decimal?)0m));
        var cn01 = rows.Single(r => r.BranchId == seeded.Id);
        (cn01.Revenue, cn01.MarginPct, cn01.Receivable, cn01.StockValue)
            .Should().Be(((decimal?)0m, (decimal?)null, (decimal?)0m, (decimal?)0m));
    }

    [Fact]
    public async Task One_branch_when_named()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetBranchesAsync(CloneAdminClient(), $"{October}&branchId={ids.MainBranchId}");

        rows.Select(r => (r.BranchId, r.Revenue)).Should().Equal((ids.MainBranchId, (decimal?)2_500m));
    }

    [Fact]
    public async Task Requires_access_all()
    {
        var user = await CreateClientWithPermissionsAsync("no_access_all", MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Reports.Profit, Permissions.Reports.Debt, Permissions.Reports.Inventory);

        (await GetExecRawAsync(user, $"branches?{October}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Columns_follow_the_domain_permissions()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("debt_only", MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Branches.AccessAll, Permissions.Reports.Debt);

        var rows = await GetBranchesAsync(user, October);

        rows.Single(r => r.BranchId == ids.MainBranchId).Receivable.Should().Be(1_500m);
        rows.Should().OnlyContain(r => r.Revenue == null && r.MarginPct == null && r.StockValue == null);
    }

    [Fact]
    public async Task Margin_needs_view_cost()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("profit_only", MainBranchId, Permissions.Dashboard.Executive,
            Permissions.Branches.AccessAll, Permissions.Reports.Profit);

        var rows = await GetBranchesAsync(user, October);

        rows.Single(r => r.BranchId == ids.MainBranchId).Revenue.Should().Be(2_500m);
        rows.Should().OnlyContain(r => r.MarginPct == null && r.Receivable == null && r.StockValue == null);
    }

    [Fact]
    public async Task Period_and_balance_date_are_the_requested_ones()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetBranchesAsync(CloneAdminClient(),
            $"from=2026-10-04&to=2026-10-31&at=2026-10-03&branchId={ids.MainBranchId}");

        // From the 4th on only the return of 10-08 is in the period; on the 3rd the receipt of 10-05 and the return
        // are not in the balance yet.
        var main = rows.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            main.Revenue.Should().Be(-500m);
            main.Receivable.Should().Be(6_000m);
        }
    }

    [Fact]
    public async Task Margin_and_stock_value_of_branches_with_tracked_stock()
    {
        // Bought 10 × 100, sold 4 × 150 unpaid: revenue 600, cost 400, 6 left worth 600, the customer owes 600.
        var (sold, _, _) = await SeedTrackedSaleAsync();
        var bought = await CreateFreshBranchAsync();
        var productE = await CreateInventoryProductAsync("BRE-" + Guid.NewGuid().ToString("N")[..8]);
        await PostLineAsync(bought, StockDirection.In, "NMH", "2026-10-02 09:00", bought.SupplierId, productE, 2, 50);

        var rows = await GetBranchesAsync(CloneAdminClient(), October);

        var soldRow = rows.Single(r => r.BranchId == sold.BranchId);
        (soldRow.Revenue, soldRow.MarginPct, soldRow.Receivable, soldRow.StockValue)
            .Should().Be(((decimal?)600m, (decimal?)33.33m, (decimal?)600m, (decimal?)600m));
        var boughtRow = rows.Single(r => r.BranchId == bought.BranchId);
        (boughtRow.Revenue, boughtRow.MarginPct, boughtRow.Receivable, boughtRow.StockValue)
            .Should().Be(((decimal?)0m, (decimal?)null, (decimal?)0m, (decimal?)100m));
    }

    [Fact]
    public async Task Stock_value_needs_reports_inventory_and_view_cost()
    {
        var (sold, _, _) = await SeedTrackedSaleAsync();
        var inventoryOnly = await CreateClientWithPermissionsAsync("inventory_only", MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Branches.AccessAll, Permissions.Reports.Inventory);
        var costOnly = await CreateClientWithPermissionsAsync("cost_only", MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Branches.AccessAll, Permissions.Inventory.ViewCost);
        var both = await CreateClientWithPermissionsAsync("inventory_cost", MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Branches.AccessAll, Permissions.Reports.Inventory,
            Permissions.Inventory.ViewCost);

        (await GetBranchesAsync(inventoryOnly, October)).Should().OnlyContain(r => r.StockValue == null);
        // Seeing costs is not seeing sales: no revenue and no margin either.
        (await GetBranchesAsync(costOnly, October))
            .Should().OnlyContain(r => r.StockValue == null && r.Revenue == null && r.MarginPct == null);
        (await GetBranchesAsync(both, October)).Single(r => r.BranchId == sold.BranchId).StockValue.Should().Be(600m);
    }

    [Fact]
    public async Task Rows_are_in_ordinal_code_order()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lower = await CreateBranchAsync("aa" + suffix);
        var upper = await CreateBranchAsync("ZZ" + suffix);

        var rows = await GetBranchesAsync(CloneAdminClient(), October);

        // Ordinal order puts upper case first: ZZ… before aa…, which a culture-aware or database order would put first.
        rows.Select(r => r.BranchCode).Should().BeInAscendingOrder(StringComparer.Ordinal);
        rows.Select(r => r.BranchId).Should().ContainInOrder(upper, lower);
    }

    private static Task<List<BranchComparisonRowDto>> GetBranchesAsync(HttpClient client, string query) =>
        GetExecAsync<List<BranchComparisonRowDto>>(client, $"branches?{query}");
}
