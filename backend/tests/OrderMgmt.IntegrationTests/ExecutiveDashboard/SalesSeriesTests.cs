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

    private static DateOnly D(int month, int day) => new(2026, month, day);

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
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainClient(ids);

        await AssertBadRequestKeyAsync(client, "sales/series?from=2025-01-01&to=2026-10-01&granularity=Day",
            "granularity", "Biểu đồ theo ngày tối đa 366 ngày. Hãy chọn theo tuần hoặc tháng.");

        (await GetExecRawAsync(client, "sales/series?from=2025-01-01&to=2026-10-01&granularity=Month"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Requires_reports_profit()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = await CreateClientWithPermissionsAsync("executive_only", ids.MainBranchId,
            Permissions.Dashboard.Executive);

        (await GetExecRawAsync(client, $"sales/series?{October}&granularity=Day"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<SalesSeriesDto> GetSeriesAsync(HttpClient client, string query) =>
        GetExecAsync<SalesSeriesDto>(client, $"sales/series?{query}");
}
