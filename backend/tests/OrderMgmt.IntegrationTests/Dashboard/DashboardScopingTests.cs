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
public class DashboardScopingTests : DashboardTestBase
{
    public DashboardScopingTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Summary_AsSales_ReturnsOnlyOwnQuotations()
    {
        await CreateTestUserAsync("dash_sale1", "Sales@123", RoleCodes.Sales);
        await CreateTestUserAsync("dash_sale2", "Sales@123", RoleCodes.Sales);
        var sale1Id = await GetUserIdAsync("dash_sale1");
        var sale2Id = await GetUserIdAsync("dash_sale2");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < 5; i++)
            await SeedQuotationAsync($"BG-S1-{i}", sale1Id, QuotationStatus.Draft, today, 100m);
        for (var i = 0; i < 5; i++)
            await SeedQuotationAsync($"BG-S2-{i}", sale2Id, QuotationStatus.Draft, today, 100m);

        await AuthenticateAsync("dash_sale1", "Sales@123");
        var resp = await _client.GetAsync($"/api/dashboard/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);
        body!.Data!.TotalCount.Value.Should().Be(5);
    }

    [Fact]
    public async Task Summary_AsAdmin_NoSaleFilter_ReturnsAll()
    {
        await CreateTestUserAsync("dash_sale_a", "Sales@123", RoleCodes.Sales);
        await CreateTestUserAsync("dash_sale_b", "Sales@123", RoleCodes.Sales);
        var saleAId = await GetUserIdAsync("dash_sale_a");
        var saleBId = await GetUserIdAsync("dash_sale_b");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < 3; i++)
            await SeedQuotationAsync($"BG-A-{i}", saleAId, QuotationStatus.Draft, today, 100m);
        for (var i = 0; i < 4; i++)
            await SeedQuotationAsync($"BG-B-{i}", saleBId, QuotationStatus.Draft, today, 100m);

        // admin is the default authenticated user
        var resp = await _client.GetAsync($"/api/dashboard/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);
        body!.Data!.TotalCount.Value.Should().Be(7);
    }

    [Fact]
    public async Task Summary_AsAdmin_WithSaleFilter_ReturnsScoped()
    {
        await CreateTestUserAsync("dash_scope1", "Sales@123", RoleCodes.Sales);
        await CreateTestUserAsync("dash_scope2", "Sales@123", RoleCodes.Sales);
        var s1 = await GetUserIdAsync("dash_scope1");
        var s2 = await GetUserIdAsync("dash_scope2");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedQuotationAsync("BG-SC1-1", s1, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-SC1-2", s1, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-SC2-1", s2, QuotationStatus.Draft, today, 100m);

        var resp = await _client.GetAsync($"/api/dashboard/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&saleUserId={s1}");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);
        body!.Data!.TotalCount.Value.Should().Be(2);
    }

    [Fact]
    public async Task Summary_AsSales_IgnoresSaleUserIdParam()
    {
        await CreateTestUserAsync("dash_ig1", "Sales@123", RoleCodes.Sales);
        await CreateTestUserAsync("dash_ig2", "Sales@123", RoleCodes.Sales);
        var s1 = await GetUserIdAsync("dash_ig1");
        var s2 = await GetUserIdAsync("dash_ig2");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedQuotationAsync("BG-IG1-1", s1, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-IG2-1", s2, QuotationStatus.Draft, today, 100m);
        await SeedQuotationAsync("BG-IG2-2", s2, QuotationStatus.Draft, today, 100m);

        await AuthenticateAsync("dash_ig1", "Sales@123");
        // Sales1 tries to view sales2's data — must still only see own data
        var resp = await _client.GetAsync($"/api/dashboard/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&saleUserId={s2}");
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<DashboardSummaryDto>>(TestJson.Options);
        body!.Data!.TotalCount.Value.Should().Be(1);
    }

    [Fact]
    public async Task Leaderboard_AsSales_Returns403()
    {
        await CreateTestUserAsync("dash_no_leader", "Sales@123", RoleCodes.Sales);
        await AuthenticateAsync("dash_no_leader", "Sales@123");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var resp = await _client.GetAsync($"/api/dashboard/sales-leaderboard?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
