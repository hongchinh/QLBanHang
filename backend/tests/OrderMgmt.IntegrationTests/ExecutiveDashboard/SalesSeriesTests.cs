using System.Net;
using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// sales/series (Phase 01, Task 1.6).
[Collection(nameof(PostgresCollection))]
public class SalesSeriesTests : ExecutiveDashboardTestBase
{
    public SalesSeriesTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Daily_points_are_zero_filled()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetSeriesAsync(MainClient(ids), $"{October}&granularity=Day&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Day);
        series.Points.Select(p => p.Date).Should().Equal(Enumerable.Range(1, 31).Select(day => D(10, day)));
        series.Points.Single(p => p.Date == D(10, 3)).Revenue.Should().Be(3_000m);
        series.Points.Single(p => p.Date == D(10, 8)).Revenue.Should().Be(-500m);
        series.Points.Where(p => p.Date != D(10, 3) && p.Date != D(10, 8)).Should().OnlyContain(p => p.Revenue == 0m);
        series.Points.Should().OnlyContain(p => p.Cost == 0m);
    }

    [Fact]
    public async Task Weekly_points_start_on_monday()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetSeriesAsync(MainClient(ids), $"{October}&granularity=Week&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Week);
        series.Points.Select(p => p.Date).Should().Equal(D(9, 28), D(10, 5), D(10, 12), D(10, 19), D(10, 26));
        series.Points[0].Revenue.Should().Be(3_000m);
        series.Points[1].Revenue.Should().Be(-500m);
    }

    [Fact]
    public async Task Monthly_point()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetSeriesAsync(MainClient(ids), $"{October}&granularity=Month&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Month);
        var point = series.Points.Should().ContainSingle().Which;
        point.Date.Should().Be(D(10, 1));
        point.Revenue.Should().Be(2_500m);
    }

    [Fact]
    public async Task Buckets_follow_vn_dates()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);
        foreach (var at in new[] { "2026-10-20 23:30", "2026-10-21 00:30" })
            await ReportDataset.PostStockAsync(this, client, StockDirection.Out, "XBH", at, ids.MainWarehouseId,
                ids.Kh1Id, 100m, paymentMethodId: null, paid: 0m);

        var series = await GetSeriesAsync(client, $"{October}&granularity=Day&branchId={ids.MainBranchId}");

        series.Points.Single(p => p.Date == D(10, 20)).Revenue.Should().Be(100m);
        series.Points.Single(p => p.Date == D(10, 21)).Revenue.Should().Be(100m);
    }

    [Fact]
    public async Task Daily_series_over_a_year_is_rejected()
    {
        var client = CloneAdminClient();

        await AssertBadRequestKeyAsync(client, "sales/series?from=2025-01-01&to=2026-10-01&granularity=Day",
            "granularity", "Biểu đồ theo ngày tối đa 366 ngày. Hãy chọn theo tuần hoặc tháng.");

        (await GetExecRawAsync(client, "sales/series?from=2025-01-01&to=2026-10-01&granularity=Month"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// Pins the framework: model binding refuses a number outside the enum by itself, before any validator runs.
    /// The body is a ValidationProblemDetails, not the app envelope, so only the status is asserted.
    [Fact]
    public async Task Undefined_granularity_number_is_rejected()
    {
        (await GetExecRawAsync(CloneAdminClient(), $"sales/series?{October}&granularity=7"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tracked_sale_has_cost_and_gross_profit_points()
    {
        var (branch, _, _) = await SeedTrackedSaleAsync();

        var series = await GetSeriesAsync(branch.Client, $"{October}&granularity=Day&branchId={branch.BranchId}");

        var sale = series.Points.Single(p => p.Date == D(10, 4));
        sale.Revenue.Should().Be(600m);
        sale.Cost.Should().Be(400m);
        sale.GrossProfit.Should().Be(200m);
        var others = series.Points.Where(p => p.Date != D(10, 4)).ToList();
        others.Should().HaveCount(30);
        others.Should().OnlyContain(p => p.Revenue == 0m && p.Cost == 0m && p.GrossProfit == 0m);
    }

    [Fact]
    public async Task Cost_is_hidden_without_view_cost()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var user = await CreateClientWithPermissionsAsync("series_no_cost", ids.MainBranchId,
            Permissions.Dashboard.Executive, Permissions.Reports.Profit);

        var series = await GetSeriesAsync(user, $"{OctoberRange}&granularity=Month");

        series.Points.Should().ContainSingle().Which.Revenue.Should().Be(2_500m);
        series.Points.Should().OnlyContain(p => p.Cost == null && p.GrossProfit == null);
    }

    [Fact]
    public async Task Requires_reports_profit()
    {
        var client = await CreateClientWithPermissionsAsync("executive_only", MainBranchId,
            Permissions.Dashboard.Executive);

        (await GetExecRawAsync(client, $"sales/series?{October}&granularity=Day"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<SalesSeriesDto> GetSeriesAsync(HttpClient client, string query) =>
        GetExecAsync<SalesSeriesDto>(client, $"sales/series?{query}");
}
