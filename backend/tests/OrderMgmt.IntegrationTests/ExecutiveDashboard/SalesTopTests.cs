using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// sales/top (Phase 01, Task 1.7).
[Collection(nameof(PostgresCollection))]
public class SalesTopTests : ExecutiveDashboardTestBase
{
    public SalesTopTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Top_customers_of_main_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Customer&branchId={ids.MainBranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(ids.Kh1Id);
        row.Code.Should().Be(ids.Kh1Code);
        row.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Top_customers_company_wide_merge_branches()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Customer");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(ids.Kh1Id);
        row.Revenue.Should().Be(12_499m);
    }

    [Fact]
    public async Task Top_products_ordered_by_revenue()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Product&branchId={ids.MainBranchId}");

        rows.Select(r => r.Revenue).Should().Equal(3_000m, -500m);
        rows.Should().OnlyContain(r => r.Code != null && r.Name != string.Empty);
    }

    [Fact]
    public async Task Top_employees()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var admin = await InDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Username == "admin")
            .Select(u => new { u.Id, u.FullName })
            .SingleAsync());

        var rows = await GetTopAsync(MainClient(ids), $"{October}&by=Employee&branchId={ids.MainBranchId}");

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(admin.Id);
        row.Name.Should().Be(admin.FullName);
        row.Code.Should().BeNull();
        row.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Sort_by_gross_profit()
    {
        var (branch, a, b) = await SeedTwoProductsAsync();
        var query = $"{October}&by=Product&branchId={branch.BranchId}";

        var byRevenue = await GetTopAsync(branch.Client, $"{query}&sort=Revenue");
        var byProfit = await GetTopAsync(branch.Client, $"{query}&sort=GrossProfit");

        byRevenue.Select(r => r.Id).Should().Equal(b, a);
        byProfit.Select(r => r.Id).Should().Equal(a, b);
        byProfit.Select(r => r.Revenue).Should().Equal(300m, 440m);
        byProfit.Select(r => r.GrossProfit).Should().Equal(100m, 40m);
    }

    [Fact]
    public async Task Gross_profit_sort_without_view_cost_falls_back_to_revenue()
    {
        var (branch, _, b) = await SeedTwoProductsAsync();
        var user = await CreateClientWithPermissionsAsync("top_no_cost", branch.BranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);

        var rows = await GetTopAsync(user, $"{October}&by=Product&sort=GrossProfit");

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be(b);
        rows.Should().OnlyContain(r => r.GrossProfit == null);
    }

    [Fact]
    public async Task Limit_is_applied_and_validated()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        var rows = await GetTopAsync(client, $"{October}&by=Product&branchId={ids.MainBranchId}&limit=1");

        rows.Should().HaveCount(1);
        await AssertBadRequestKeyAsync(client, $"sales/top?{October}&by=Product&limit=0", "limit", "Số dòng từ 1 đến 50.");
        await AssertBadRequestKeyAsync(client, $"sales/top?{October}&by=Product&limit=51", "limit", "Số dòng từ 1 đến 50.");
    }

    /// Pins the framework: model binding refuses a number outside the enum by itself, before any validator runs.
    /// The body is a ValidationProblemDetails, not the app envelope, so only the status is asserted.
    [Fact]
    public async Task Undefined_by_or_sort_number_is_rejected()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        (await GetExecRawAsync(client, $"sales/top?{October}&by=7")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetExecRawAsync(client, $"sales/top?{October}&by=Customer&sort=7"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// A fresh branch with two tracked products (VAT 0), each bought 10 × 100. A is sold 2 × 150 (revenue 300, cost 200,
    /// profit 100) and B 4 × 110 (revenue 440, cost 400, profit 40).
    private async Task<(FreshBranch Branch, Guid ProductA, Guid ProductB)> SeedTwoProductsAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var a = await CreateInventoryProductAsync("TOP-A");
        var b = await CreateInventoryProductAsync("TOP-B");
        foreach (var product in new[] { a, b })
            await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, a, 2, 150);
        await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId, b, 4, 110);
        return (branch, a, b);
    }

    private static Task<List<SalesTopRowDto>> GetTopAsync(HttpClient client, string query) =>
        GetExecAsync<List<SalesTopRowDto>>(client, $"sales/top?{query}");
}
