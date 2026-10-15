using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Reports.QuotationRevenue.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Dashboard;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Reports;

/// The Doanh thu report keeps the quotation revenue of the old overview dashboard (D10): the rule, the "Ngày ghi
/// nhận doanh thu" setting and the owner scope are the same, only the endpoints moved to /api/reports/revenue.
[Collection(nameof(PostgresCollection))]
public class QuotationRevenueReportTests : DashboardTestBase
{
    public QuotationRevenueReportTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task RevenueSeries_Day_FillsZeroGaps()
    {
        await CreateTestUserAsync("dash_rs", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_rs");
        var d1 = new DateOnly(2026, 5, 1);
        var d5 = new DateOnly(2026, 5, 5);
        var d7 = new DateOnly(2026, 5, 7);

        await SeedQuotationAsync("BG-RS-1", sale, QuotationStatus.Confirmed, d1, 100m, confirmedAt: new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        await SeedQuotationAsync("BG-RS-2", sale, QuotationStatus.Confirmed, d5, 200m, confirmedAt: new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc));
        await SeedQuotationAsync("BG-RS-3", sale, QuotationStatus.Confirmed, d7, 300m, confirmedAt: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));

        var resp = await _client.GetAsync("/api/reports/revenue/series?from=2026-05-01&to=2026-05-07&granularity=day");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<QuotationRevenueSeriesDto>>(TestJson.Options);
        body!.Data!.Points.Should().HaveCount(7);
        body.Data.Points.Sum(p => p.Total).Should().Be(600m);
        body.Data.Points.First(p => p.Date == new DateOnly(2026, 5, 2)).Total.Should().Be(0m);
    }

    [Fact]
    public async Task TopCustomers_OrdersByRevenueDesc_LimitsCorrectly()
    {
        await CreateTestUserAsync("dash_tc", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_tc");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        // Multiple quotations per customer with varying revenue. Customer A wins.
        await SeedQuotationAsync("BG-TC-A1", sale, QuotationStatus.Confirmed, today, 1000m,
            confirmedAt: now, customerName: "Khách A");
        await SeedQuotationAsync("BG-TC-A2", sale, QuotationStatus.Confirmed, today, 1000m,
            confirmedAt: now, customerName: "Khách A");
        await SeedQuotationAsync("BG-TC-B", sale, QuotationStatus.Confirmed, today, 500m,
            confirmedAt: now, customerName: "Khách B");
        await SeedQuotationAsync("BG-TC-C", sale, QuotationStatus.Confirmed, today, 100m,
            confirmedAt: now, customerName: "Khách C");

        var resp = await _client.GetAsync($"/api/reports/revenue/top-customers?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&limit=2");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<QuotationTopCustomerDto>>>(TestJson.Options);
        body!.Data!.Should().HaveCount(2);
        body.Data![0].Revenue.Should().BeGreaterThanOrEqualTo(body.Data![1].Revenue);
    }

    [Fact]
    public async Task RevenueRule_ExcludesCancelled()
    {
        await CreateTestUserAsync("dash_rr", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_rr");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        await SeedQuotationAsync("BG-RR-1", sale, QuotationStatus.Confirmed, today, 1000m, confirmedAt: now);
        // Cancelled-after-confirm row must NOT contribute to revenue.
        await SeedQuotationAsync("BG-RR-2", sale, QuotationStatus.Confirmed, today, 500m,
            confirmedAt: now, cancelledAt: now);

        var resp = await _client.GetAsync($"/api/reports/revenue/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<QuotationRevenueSummaryDto>>(TestJson.Options);
        body!.Data!.RangeRevenue.Value.Should().Be(1000m);
    }

    [Fact]
    public async Task Summary_counts_confirmed_quotations()
    {
        await CreateTestUserAsync("qrr_sum", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("qrr_sum");
        var day = new DateOnly(2031, 3, 15);

        // 2031-03-15 is a day no other test uses: the database is shared. The confirmation falls on the same UTC
        // day, so the quotation counts in every date mode.
        await SeedQuotationAsync("QRR-SUM-1", sale, QuotationStatus.Confirmed, day, 100m,
            confirmedAt: new DateTime(2031, 3, 15, 3, 0, 0, DateTimeKind.Utc));
        await SeedQuotationAsync("QRR-SUM-2", sale, QuotationStatus.Draft, day, 50m);

        var resp = await _client.GetAsync("/api/reports/revenue/summary?from=2031-03-15&to=2031-03-15");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<QuotationRevenueSummaryDto>>(TestJson.Options);
        body!.Data!.RangeRevenue.Value.Should().Be(100m);
        body.Data.TotalCount.Value.Should().Be(2);
    }

    [Fact]
    public async Task Sales_role_without_reports_revenue_gets_403()
    {
        await CreateTestUserAsync("qrr_noperm", "Sales@123", RoleCodes.Sales);
        await AuthenticateAsync("qrr_noperm", "Sales@123");

        var resp = await _client.GetAsync("/api/reports/revenue/summary?from=2031-03-15&to=2031-03-15");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SalesLeaderboard_AsAdmin_OrdersByRevenueDesc()
    {
        await CreateTestUserAsync("qrr_lead_low", "Sales@123", RoleCodes.Sales);
        await CreateTestUserAsync("qrr_lead_high", "Sales@123", RoleCodes.Sales);
        var low = await GetUserIdAsync("qrr_lead_low");
        var high = await GetUserIdAsync("qrr_lead_high");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        await SeedQuotationAsync("BG-QLD-L", low, QuotationStatus.Confirmed, today, 100m, confirmedAt: now);
        await SeedQuotationAsync("BG-QLD-H1", high, QuotationStatus.Confirmed, today, 500m, confirmedAt: now);
        await SeedQuotationAsync("BG-QLD-H2", high, QuotationStatus.Confirmed, today, 500m, confirmedAt: now);

        var resp = await _client.GetAsync($"/api/reports/revenue/sales-leaderboard?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<QuotationSalesLeaderboardItemDto>>>(TestJson.Options);
        var data = body!.Data!;
        data.Should().NotBeEmpty();
        var highRow = data.FirstOrDefault(x => x.UserId == high);
        var lowRow = data.FirstOrDefault(x => x.UserId == low);
        highRow.Should().NotBeNull();
        lowRow.Should().NotBeNull();
        data.IndexOf(highRow!).Should().BeLessThan(data.IndexOf(lowRow!));
    }

    [Fact]
    public async Task SalesLeaderboard_without_quotations_view_all_gets_403()
    {
        await CreateTestUserAsync("qrr_no_leader", "Sales@123", RoleCodes.Sales);
        await AuthenticateAsync("qrr_no_leader", "Sales@123");

        var resp = await _client.GetAsync("/api/reports/revenue/sales-leaderboard?from=2031-03-15&to=2031-03-15");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
