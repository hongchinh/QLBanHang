using FluentAssertions;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// cash/series (Phase 02, Task 2.2).
[Collection(nameof(PostgresCollection))]
public class CashSeriesTests : ExecutiveDashboardTestBase
{
    public CashSeriesTests(PostgresFixture pg) : base(pg) { }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Daily_points()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetCashSeriesAsync(MainClient(ids), $"{October}&granularity=Day&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Day);
        series.Points.Select(p => p.Date).Should().Equal(Enumerable.Range(1, 31).Select(day => D(10, day)));
        // Every other day is zero, among them 10-15 (the fund transfer) and 10-18 (the cancelled receipt).
        var expected = Enumerable.Range(1, 31).ToDictionary(day => day, _ => (Receipts: 0m, Payments: 0m));
        expected[3] = (1_000m, 0m);
        expected[5] = (4_000m, 0m);
        expected[10] = (0m, 2_000m);
        expected[12] = (0m, 3_000m);
        expected[16] = (0m, 700m);
        series.Points.Select(p => (p.Date.Day, p.Receipts, p.Payments))
            .Should().Equal(expected.Select(e => (e.Key, e.Value.Receipts, e.Value.Payments)));
    }

    [Fact]
    public async Task Monthly_point()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetCashSeriesAsync(MainClient(ids), $"{October}&granularity=Month&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Month);
        var point = series.Points.Should().ContainSingle().Which;
        point.Date.Should().Be(D(10, 1));
        point.Receipts.Should().Be(5_000m);
        point.Payments.Should().Be(5_700m);
    }

    [Fact]
    public async Task Weekly_points_start_on_monday()
    {
        var ids = await ReportDataset.SeedAsync(this);

        var series = await GetCashSeriesAsync(MainClient(ids), $"{October}&granularity=Week&branchId={ids.MainBranchId}");

        series.Granularity.Should().Be(SeriesGranularity.Week);
        series.Points.Select(p => (p.Date, p.Receipts, p.Payments)).Should().Equal(
            (D(9, 28), 1_000m, 0m), (D(10, 5), 4_000m, 2_000m), (D(10, 12), 0m, 3_700m), (D(10, 19), 0m, 0m),
            (D(10, 26), 0m, 0m));
    }

    [Fact]
    public async Task Buckets_follow_vn_dates()
    {
        var ids = await ReportDataset.SeedAsync(this);
        // 23:30 and 00:30 in Vietnam are on the same UTC day but on two VN days.
        await InsertCashVoucherAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Receipt, 100m, Vn("2026-10-20 23:30"));
        await InsertCashVoucherAsync(ids.MainBranchId, ids.QtmId, CashVoucherType.Payment, 30m, Vn("2026-10-21 00:30"));

        var series = await GetCashSeriesAsync(MainClient(ids), $"{October}&granularity=Day&branchId={ids.MainBranchId}");

        series.Points.Single(p => p.Date == D(10, 20)).Receipts.Should().Be(100m);
        series.Points.Single(p => p.Date == D(10, 20)).Payments.Should().Be(0m);
        series.Points.Single(p => p.Date == D(10, 21)).Receipts.Should().Be(0m);
        series.Points.Single(p => p.Date == D(10, 21)).Payments.Should().Be(30m);
    }

    [Fact]
    public async Task Weekly_points_count_only_the_period()
    {
        var branch = await CreateFreshBranchAsync();
        var fundId = await CashFundIdAsync(branch.BranchId);
        // The first and the last week of October also hold days of September and November.
        await InsertCashVoucherAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 5m, Vn("2026-09-30 23:59"));
        await InsertCashVoucherAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 10m, Vn("2026-10-01 00:00"));
        await InsertCashVoucherAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 20m, Vn("2026-10-31 23:59"));
        await InsertCashVoucherAsync(branch.BranchId, fundId, CashVoucherType.Receipt, 40m, Vn("2026-11-01 00:00"));

        var series = await GetCashSeriesAsync(branch.Client, $"{October}&granularity=Week&branchId={branch.BranchId}");

        series.Points.Select(p => (p.Date, p.Receipts)).Should().Equal(
            (D(9, 28), 10m), (D(10, 5), 0m), (D(10, 12), 0m), (D(10, 19), 0m), (D(10, 26), 20m));
    }

    private static Task<CashSeriesDto> GetCashSeriesAsync(HttpClient client, string query) =>
        GetExecAsync<CashSeriesDto>(client, $"cash/series?{query}");
}
