using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Catalog.Products.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;
using Xunit;

namespace OrderMgmt.IntegrationTests.Catalog;

[Collection(nameof(PostgresCollection))]
public class ProductInventoryFieldsTests : QuotationTestBase
{
    public ProductInventoryFieldsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Inventory_fields_default_roundtrip_and_appear_in_search()
    {
        var groupId = await GroupIdAsync("EPS");
        var created = await ReadAsync<ProductDto>(await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Code = "INV-1", Name = "Tấm INV", ProductGroupId = groupId, UnitId = _unitId,
            DefaultTaxRate = 8, Length = 1000, Width = 500, Thickness = 20,
        }));
        created.TrackInventory.Should().BeTrue();
        created.PurchaseDiscountRate.Should().Be(0);
        created.SalesDiscountRate.Should().Be(0);
        created.PriceIncludesVat.Should().BeFalse();

        var updated = await ReadAsync<ProductDto>(await _client.PutAsJsonAsync($"/api/products/{created.Id}", new UpdateProductRequest
        {
            Name = "Tấm INV", ProductGroupId = groupId, UnitId = _unitId, Status = ProductStatus.Active,
            DefaultTaxRate = 8, Length = 1000, Width = 500, Thickness = 20,
            TrackInventory = false, PurchaseDiscountRate = 5, SalesDiscountRate = 3, PriceIncludesVat = true,
        }));
        updated.TrackInventory.Should().BeFalse();
        updated.PurchaseDiscountRate.Should().Be(5);
        updated.SalesDiscountRate.Should().Be(3);
        updated.PriceIncludesVat.Should().BeTrue();

        var suggestions = await ReadAsync<List<ProductSuggestionDto>>(await _client.GetAsync("/api/products/search?q=INV-1"));
        var hit = suggestions.Single(s => s.Code == "INV-1");
        hit.DefaultTaxRate.Should().Be(8);
        hit.Length.Should().Be(1000);
        hit.Width.Should().Be(500);
        hit.Thickness.Should().Be(20);
        hit.TrackInventory.Should().BeFalse();
        hit.PurchaseDiscountRate.Should().Be(5);
        hit.SalesDiscountRate.Should().Be(3);
        hit.PriceIncludesVat.Should().BeTrue();
    }

    [Fact]
    public async Task Discount_rate_above_100_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Code = "INV-2", Name = "Tấm INV 2", ProductGroupId = await GroupIdAsync("EPS"), UnitId = _unitId,
            PurchaseDiscountRate = 101,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Untracked_product_is_persisted_as_untracked()
    {
        var created = await ReadAsync<ProductDto>(await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Code = "INV-SVC", Name = "Phí vận chuyển", ProductGroupId = await GroupIdAsync("VC"), UnitId = _unitId,
            TrackInventory = false, DefaultTaxRate = 8,
        }));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Products.AsNoTracking().SingleAsync(p => p.Id == created.Id)).TrackInventory.Should().BeFalse();
        }

        var list = await ReadAsync<PagedResult<ProductListItemDto>>(await _client.GetAsync("/api/products?search=INV-SVC"));
        var row = list.Items.Single(p => p.Code == "INV-SVC");
        row.TrackInventory.Should().BeFalse();
        row.DefaultTaxRate.Should().Be(8);
    }

    private async Task<Guid> GroupIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.ProductGroups.FirstAsync(g => g.Code == code)).Id;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(body, TestJson.Options)!.Data!;
    }
}
