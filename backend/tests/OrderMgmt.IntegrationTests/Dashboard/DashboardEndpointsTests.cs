using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Dashboard;

[Collection(nameof(PostgresCollection))]
public class DashboardEndpointsTests : DashboardTestBase
{
    public DashboardEndpointsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Funnel_OnlyCountsWithinRange()
    {
        await CreateTestUserAsync("dash_f", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_f");
        var inside = new DateOnly(2026, 5, 5);
        var outside = new DateOnly(2026, 4, 30);

        await SeedQuotationAsync("BG-F-IN", sale, QuotationStatus.Confirmed, inside, 100m,
            confirmedAt: new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc));
        await SeedQuotationAsync("BG-F-OUT", sale, QuotationStatus.Confirmed, outside, 100m,
            confirmedAt: new DateTime(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc));

        var resp = await _client.GetAsync("/api/dashboard/summary?from=2026-05-01&to=2026-05-15");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);
        body!.Data!.Funnel.Confirmed.Should().Be(1);
    }

    [Fact]
    public async Task RecentActivity_EmptyDb_Returns200WithEmptyList()
    {
        var resp = await _client.GetAsync("/api/dashboard/recent-activity?limit=8");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<ActivityItemDto>>>(TestJson.Options);
        body!.Data.Should().NotBeNull();
        body.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task RecentActivity_MergesAllSources_OrdersByTimeDesc_RespectsLimit()
    {
        await CreateTestUserAsync("dash_ra", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_ra");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Created (no terminal event) — only contributes a "created" entry.
        await SeedQuotationAsync("BG-RA-C", sale, QuotationStatus.Draft, today, 100m);
        // Confirmed — contributes both "created" and "confirmed" entries.
        await SeedQuotationAsync("BG-RA-F", sale, QuotationStatus.Confirmed, today, 200m,
            confirmedAt: DateTime.UtcNow.AddMinutes(-5));
        // Cancelled (newest event) — contributes "created" and "cancelled".
        await SeedQuotationAsync("BG-RA-X", sale, QuotationStatus.Cancelled, today, 300m,
            cancelledAt: DateTime.UtcNow);

        var resp = await _client.GetAsync("/api/dashboard/recent-activity?limit=8");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<ActivityItemDto>>>(TestJson.Options);
        var items = body!.Data!;

        items.Should().NotBeEmpty();
        items.Select(i => i.Type).Should().Contain(new[] { "created", "confirmed", "cancelled" });
        items.Should().BeInDescendingOrder(i => i.At);
        items.Count.Should().BeLessThanOrEqualTo(8);

        var cancelledRow = items.FirstOrDefault(i => i.Type == "cancelled");
        cancelledRow.Should().NotBeNull();
        cancelledRow!.Code.Should().Be("BG-RA-X");
        cancelledRow.Amount.Should().Be(300m);
    }

    [Fact]
    public async Task TotalCount_spark_counts_quotations_by_date()
    {
        await CreateTestUserAsync("dash_sp", "Sales@123", RoleCodes.Sales);
        var sale = await GetUserIdAsync("dash_sp");
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Drafts: the spark counts quotations by QuotationDate, not only those that pass the revenue rule.
        await SeedQuotationAsync("BG-SP-1", sale, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-SP-2", sale, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-SP-3", sale, QuotationStatus.Draft, today.AddDays(-2), 100m);

        await AuthenticateAsync("dash_sp", "Sales@123");
        var resp = await _client.GetAsync($"/api/dashboard/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);

        body!.Data!.TotalCount.Spark.Should().Equal(0m, 0m, 0m, 0m, 1m, 0m, 2m);
    }
}
