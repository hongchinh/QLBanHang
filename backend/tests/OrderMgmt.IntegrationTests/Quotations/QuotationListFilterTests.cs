using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Quotations;

[Collection(nameof(PostgresCollection))]
public class QuotationListFilterTests : QuotationTestBase
{
    public QuotationListFilterTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task List_with_multi_status_returns_quotations_in_any_listed_status()
    {
        var draftId = await CreateQuotationAsync();
        var sentId = await CreateQuotationAsync();
        await TransitionAsync(sentId, QuotationAction.Send);
        var confirmedId = await CreateQuotationAsync();
        await TransitionAsync(confirmedId, QuotationAction.Send);
        await TransitionAsync(confirmedId, QuotationAction.Confirm);

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Draft,Sent&pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().Contain(x => x.Id == draftId);
        response.Data.Items.Should().Contain(x => x.Id == sentId);
        response.Data.Items.Should().NotContain(x => x.Id == confirmedId);
    }

    [Fact]
    public async Task List_with_single_status_legacy_still_works()
    {
        var draftId = await CreateQuotationAsync();
        var sentId = await CreateQuotationAsync();
        await TransitionAsync(sentId, QuotationAction.Send);

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Draft&pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().Contain(x => x.Id == draftId);
        response.Data.Items.Should().NotContain(x => x.Id == sentId);
    }

    [Fact]
    public async Task List_with_invalid_status_returns_400()
    {
        var response = await _client.GetAsync("/api/quotations?status=Invalid");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_includes_subtotal_discount_freight_fields()
    {
        var id = await CreateQuotationAsync();

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=10", TestJson.Options);

        response!.Data!.Items.Should()
            .ContainSingle(x => x.Id == id)
            .Which.Should().Match<QuotationListItemDto>(x =>
                x.Subtotal >= 0 && x.Discount >= 0 && x.Freight >= 0);
    }

    [Fact]
    public async Task List_aggregates_sum_across_all_filtered_records_not_just_current_page()
    {
        // 25 báo giá Draft, mỗi báo qty=1, price=100, taxRate=0 → subtotal=total=100.
        for (var i = 0; i < 25; i++)
            await CreateSimpleQuotationAsync(quantity: 1, unitPrice: 100);

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=10", TestJson.Options);

        response!.Data!.Items.Should().HaveCount(10);
        response.Data.Aggregates.Subtotal.Should().Be(25 * 100m);
        response.Data.Aggregates.Total.Should().Be(25 * 100m);
    }

    [Fact]
    public async Task List_aggregates_respect_status_filter()
    {
        // 3 Draft + 2 Sent (mỗi báo subtotal=100).
        for (var i = 0; i < 3; i++)
            await CreateSimpleQuotationAsync(1, 100);
        for (var i = 0; i < 2; i++)
        {
            var id = await CreateSimpleQuotationAsync(1, 100);
            await TransitionAsync(id, QuotationAction.Send);
        }

        var draftRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Draft&pageSize=100", TestJson.Options);
        draftRes!.Data!.Aggregates.Subtotal.Should().Be(300m);

        var sentRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Sent&pageSize=100", TestJson.Options);
        sentRes!.Data!.Aggregates.Subtotal.Should().Be(200m);
    }

    [Fact]
    public async Task List_aggregates_respect_owner_scope()
    {
        // Admin (role admin, has view_all): 3 báo giá; Sale user: 2 báo giá.
        for (var i = 0; i < 3; i++)
            await CreateSimpleQuotationAsync(1, 100);

        await CreateTestUserAsync("sale_owner_scope", "Sale@123", "SALES");
        await AuthenticateAsync("sale_owner_scope", "Sale@123");
        for (var i = 0; i < 2; i++)
            await CreateSimpleQuotationAsync(1, 100);

        // Sale user thấy chỉ 2 báo giá của mình.
        var saleRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);
        saleRes!.Data!.Aggregates.Subtotal.Should().Be(200m);

        // Admin thấy tất cả 5.
        await AuthenticateAsync("admin", "Admin@123");
        var adminRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);
        adminRes!.Data!.Aggregates.Subtotal.Should().Be(500m);
    }

    [Fact]
    public async Task List_empty_result_returns_zero_aggregates()
    {
        await CreateSimpleQuotationAsync(1, 100);

        // Filter không match (from = future date) → empty.
        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?from=2030-01-01&pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().BeEmpty();
        response.Data.Aggregates.Subtotal.Should().Be(0m);
        response.Data.Aggregates.Total.Should().Be(0m);
    }

    [Fact]
    public async Task List_aggregates_exclude_cancelled_when_no_status_filter()
    {
        // 3 Draft active + 2 Cancelled (mỗi báo subtotal=100).
        for (var i = 0; i < 3; i++)
            await CreateSimpleQuotationAsync(1, 100);
        for (var i = 0; i < 2; i++)
        {
            var id = await CreateSimpleQuotationAsync(1, 100);
            await TransitionAsync(id, QuotationAction.Cancel);
        }

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().HaveCount(5);
        response.Data.Aggregates.Subtotal.Should().Be(300m);
    }

    [Fact]
    public async Task List_aggregates_include_cancelled_when_explicitly_filtered()
    {
        // 3 Draft + 2 Cancelled (mỗi báo subtotal=100).
        for (var i = 0; i < 3; i++)
            await CreateSimpleQuotationAsync(1, 100);
        for (var i = 0; i < 2; i++)
        {
            var id = await CreateSimpleQuotationAsync(1, 100);
            await TransitionAsync(id, QuotationAction.Cancel);
        }

        var cancelledOnly = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Cancelled&pageSize=100", TestJson.Options);
        cancelledOnly!.Data!.Items.Should().HaveCount(2);
        cancelledOnly.Data.Aggregates.Subtotal.Should().Be(200m);

        var bothExplicit = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Cancelled,Draft&pageSize=100", TestJson.Options);
        bothExplicit!.Data!.Items.Should().HaveCount(5);
        bothExplicit.Data.Aggregates.Subtotal.Should().Be(500m);
    }

    [Fact]
    public async Task List_admin_filter_ownerUserIds_returns_union_of_two_owners()
    {
        // Admin: 2 quotations.
        await CreateSimpleQuotationAsync(1, 100);
        await CreateSimpleQuotationAsync(1, 100);

        // sale_owner_filter_a: 3 quotations.
        await CreateTestUserAsync("sale_owner_filter_a", "Sale@123", "SALES");
        await AuthenticateAsync("sale_owner_filter_a", "Sale@123");
        for (var i = 0; i < 3; i++) await CreateSimpleQuotationAsync(1, 100);

        // sale_owner_filter_b: 2 quotations.
        await CreateTestUserAsync("sale_owner_filter_b", "Sale@123", "SALES");
        await AuthenticateAsync("sale_owner_filter_b", "Sale@123");
        for (var i = 0; i < 2; i++) await CreateSimpleQuotationAsync(1, 100);

        await AuthenticateAsync("admin", "Admin@123");
        var ownerIdA = await GetUserIdAsync("sale_owner_filter_a");
        var ownerIdB = await GetUserIdAsync("sale_owner_filter_b");

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            $"/api/quotations?ownerUserIds={ownerIdA},{ownerIdB}&pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().HaveCount(5);
        response.Data.Aggregates.Subtotal.Should().Be(500m);
    }

    [Fact]
    public async Task List_admin_without_ownerUserIds_returns_all_visible_quotations()
    {
        // Admin: 2 quotations.
        await CreateSimpleQuotationAsync(1, 100);
        await CreateSimpleQuotationAsync(1, 100);

        // Sale: 2 quotations.
        await CreateTestUserAsync("sale_no_owner_filter", "Sale@123", "SALES");
        await AuthenticateAsync("sale_no_owner_filter", "Sale@123");
        for (var i = 0; i < 2; i++) await CreateSimpleQuotationAsync(1, 100);

        await AuthenticateAsync("admin", "Admin@123");

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task List_sale_forging_ownerUserIds_still_sees_only_own_quotations()
    {
        // Admin: 1 quotation (id captured for negative assertion).
        var adminQuoteId = await CreateSimpleQuotationAsync(1, 100);

        await CreateTestUserAsync("sale_forge", "Sale@123", "SALES");
        await AuthenticateAsync("sale_forge", "Sale@123");
        await CreateSimpleQuotationAsync(1, 100);
        await CreateSimpleQuotationAsync(1, 100);

        var adminUserId = await GetUserIdAsync("admin");

        var response = await _client.GetAsync($"/api/quotations?ownerUserIds={adminUserId}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<QuotationListResult>>(TestJson.Options);
        body!.Data!.Items.Should().HaveCount(2);
        body.Data.Items.Should().NotContain(x => x.Id == adminQuoteId);
    }

    [Fact]
    public async Task List_with_invalid_ownerUserIds_returns_400()
    {
        var response = await _client.GetAsync("/api/quotations?ownerUserIds=abc,def");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_aggregates_respect_owner_filter()
    {
        // Admin: 3 quotations × 100 = 300.
        for (var i = 0; i < 3; i++) await CreateSimpleQuotationAsync(1, 100);

        // sale_agg_owner: 2 quotations × 100 = 200.
        await CreateTestUserAsync("sale_agg_owner", "Sale@123", "SALES");
        await AuthenticateAsync("sale_agg_owner", "Sale@123");
        for (var i = 0; i < 2; i++) await CreateSimpleQuotationAsync(1, 100);

        await AuthenticateAsync("admin", "Admin@123");
        var saleAggOwnerId = await GetUserIdAsync("sale_agg_owner");

        var response = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            $"/api/quotations?ownerUserIds={saleAggOwnerId}&pageSize=100", TestJson.Options);

        response!.Data!.Items.Should().HaveCount(2);
        response.Data.Aggregates.Subtotal.Should().Be(200m);
    }

    [Fact]
    public async Task List_item_includes_advance_payment_field()
    {
        var req = BuildRequest();
        req.AdvancePayment = 500_000m;
        var res = await _client.PostAsJsonAsync("/api/quotations", req);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var listRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);

        listRes!.Data!.Items.Should()
            .ContainSingle(x => x.Id == id)
            .Which.AdvancePayment.Should().Be(500_000m);
    }

    [Fact]
    public async Task List_aggregates_sum_advance_payment()
    {
        var req1 = BuildRequest();
        req1.AdvancePayment = 200_000m;
        req1.TaxRate = 0;
        req1.Discount = 0;
        req1.Freight = 0;
        (await _client.PostAsJsonAsync("/api/quotations", req1)).EnsureSuccessStatusCode();

        var req2 = BuildRequest();
        req2.AdvancePayment = 300_000m;
        req2.TaxRate = 0;
        req2.Discount = 0;
        req2.Freight = 0;
        (await _client.PostAsJsonAsync("/api/quotations", req2)).EnsureSuccessStatusCode();

        var listRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?status=Draft&pageSize=100", TestJson.Options);

        listRes!.Data!.Aggregates.AdvancePayment.Should().Be(500_000m);
    }

    [Fact]
    public async Task List_item_includes_delivery_date_and_revenue_date()
    {
        var req = BuildRequest();
        req.DeliveryDate = new DateOnly(2026, 6, 15);
        var res = await _client.PostAsJsonAsync("/api/quotations", req);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        var id = created!.Data!.Id;

        var listRes = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?pageSize=100", TestJson.Options);

        var item = listRes!.Data!.Items.Single(x => x.Id == id);
        item.DeliveryDate.Should().Be(new DateOnly(2026, 6, 15));
        item.RevenueDate.Should().NotBeNull();
    }

    [Fact]
    public async Task List_filter_by_delivery_date_range_returns_matching_quotations()
    {
        var req1 = BuildRequest();
        req1.DeliveryDate = new DateOnly(2026, 6, 10);
        var r1 = await _client.PostAsJsonAsync("/api/quotations", req1);
        r1.EnsureSuccessStatusCode();
        var id1 = (await r1.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

        var req2 = BuildRequest();
        req2.DeliveryDate = new DateOnly(2026, 8, 20);
        var r2 = await _client.PostAsJsonAsync("/api/quotations", req2);
        r2.EnsureSuccessStatusCode();
        var id2 = (await r2.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

        var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?deliveryDateFrom=2026-06-01&deliveryDateTo=2026-06-30&pageSize=100",
            TestJson.Options);

        res!.Data!.Items.Should().Contain(x => x.Id == id1);
        res.Data.Items.Should().NotContain(x => x.Id == id2);
    }

    [Fact]
    public async Task List_filter_by_revenue_date_range_uses_system_date_mode()
    {
        var req1 = BuildRequest();
        req1.QuotationDate = new DateOnly(2025, 3, 15);
        var r1 = await _client.PostAsJsonAsync("/api/quotations", req1);
        r1.EnsureSuccessStatusCode();
        var id1 = (await r1.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

        var req2 = BuildRequest();
        req2.QuotationDate = new DateOnly(2025, 5, 10);
        var r2 = await _client.PostAsJsonAsync("/api/quotations", req2);
        r2.EnsureSuccessStatusCode();
        var id2 = (await r2.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

        var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?revenueDateFrom=2025-03-01&revenueDateTo=2025-03-31&pageSize=100",
            TestJson.Options);

        res!.Data!.Items.Should().Contain(x => x.Id == id1);
        res.Data.Items.Should().NotContain(x => x.Id == id2);
    }

    [Fact]
    public async Task List_filter_by_revenue_date_excludes_cancelled_quotations()
    {
        var req = BuildRequest();
        req.QuotationDate = new DateOnly(2025, 4, 10);
        var r = await _client.PostAsJsonAsync("/api/quotations", req);
        r.EnsureSuccessStatusCode();
        var id = (await r.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options))!.Data!.Id;

        var cancelRes = await _client.PostAsJsonAsync(
            $"/api/quotations/{id}/transition",
            new TransitionQuotationRequest { Action = QuotationAction.Cancel });
        cancelRes.EnsureSuccessStatusCode();

        var res = await _client.GetFromJsonAsync<ApiResponse<QuotationListResult>>(
            "/api/quotations?revenueDateFrom=2025-04-01&revenueDateTo=2025-04-30&pageSize=100",
            TestJson.Options);

        res!.Data!.Items.Should().NotContain(x => x.Id == id);
    }

    private async Task<Guid> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Username == username).Select(u => u.Id).FirstAsync();
    }

    private async Task<Guid> CreateQuotationAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/quotations", BuildRequest());
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        return created!.Data!.Id;
    }

    private async Task<Guid> CreateSimpleQuotationAsync(decimal quantity, decimal unitPrice)
    {
        var req = BuildRequest(new UpsertQuotationLineRequest
        {
            SortOrder = 0,
            ProductId = _productId,
            ProductName = "Test EPS 1000x2000",
            UnitName = "Tấm",
            PricingMode = PricingMode.PerUnit,
            Quantity = quantity,
            UnitPrice = unitPrice,
        });
        req.TaxRate = 0;
        req.Discount = 0;
        req.Freight = 0;

        var res = await _client.PostAsJsonAsync("/api/quotations", req);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>(TestJson.Options);
        return created!.Data!.Id;
    }

    private async Task TransitionAsync(Guid id, QuotationAction action)
    {
        var req = new TransitionQuotationRequest { Action = action };
        var res = await _client.PostAsJsonAsync($"/api/quotations/{id}/transition", req);
        res.EnsureSuccessStatusCode();
    }
}
