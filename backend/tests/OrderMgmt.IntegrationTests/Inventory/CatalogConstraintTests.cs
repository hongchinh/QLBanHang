using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Products.Models;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class CatalogConstraintTests : InventoryEngineTestBase
{
    public CatalogConstraintTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Product_with_activity_is_locked_except_its_name()
    {
        var p = await CreateInventoryProductAsync("CC01");
        await ReplaceAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 5, 500_000));
        var otherUnitId = await InDbAsync(db => db.Units.Where(u => u.Id != _unitId).Select(u => u.Id).FirstAsync());

        (await PutProductAsync(p, r => r.PricingMode = PricingMode.PerSquareMeter)).Should().Be(HttpStatusCode.Conflict);
        (await PutProductAsync(p, r => r.UnitId = otherUnitId)).Should().Be(HttpStatusCode.Conflict);
        (await PutProductAsync(p, r => r.TrackInventory = false)).Should().Be(HttpStatusCode.Conflict);
        (await PutProductAsync(p, r => r.PriceIncludesVat = true)).Should().Be(HttpStatusCode.Conflict);
        (await PutProductAsync(p, r => r.Name = "Tên mới")).Should().Be(HttpStatusCode.OK);

        (await _client.DeleteAsync($"/api/products/{p}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var product = await ReadDataAsync<ProductDto>(await _client.GetAsync($"/api/products/{p}"));
        product.Name.Should().Be("Tên mới");
        product.HasInventoryActivity.Should().BeTrue();
    }

    [Fact]
    public async Task Catalog_entries_in_use_cannot_be_deleted()
    {
        var p = await CreateInventoryProductAsync("CC02");
        var warehouseId = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await ReplaceAsync(InDraft(p, warehouseId, "2026-10-01 08:00", 5, 500_000));
        var otherBranchId = await CreateBranchAsync("CN02");

        (await _client.DeleteAsync($"/api/warehouses/{warehouseId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var move = await _client.PutAsJsonAsync($"/api/warehouses/{warehouseId}",
            new UpdateWarehouseRequest { Name = "Kho KHO02", BranchId = otherBranchId, IsActive = true });
        move.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var supplierId = await CreatePartnerAsync("NCC01", isCustomer: false, isSupplier: true);
        var reason = new StockReason { Code = "LD01", Name = "Nhập thử", Direction = StockDirection.In, PartnerType = PartnerType.Any };
        var method = new PaymentMethod { Code = "PM01", Name = "Ví điện tử" };
        await InDbAsync(async db =>
        {
            db.StockReasons.Add(reason);
            db.PaymentMethods.Add(method);
            db.StockVouchers.Add(new StockVoucher
            {
                Type = StockDirection.In,
                Code = "PN00001",
                VoucherAt = Vn("2026-10-01 08:00"),
                BranchId = MainBranchId,
                WarehouseId = DefaultWarehouseId,
                PartnerId = supplierId,
                ReasonId = reason.Id,
                PaymentMethodId = method.Id,
                OwnerUserId = await db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync(),
            });
            await db.SaveChangesAsync();
        });

        (await _client.DeleteAsync($"/api/suppliers/{supplierId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.DeleteAsync($"/api/stock-reasons/{reason.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.DeleteAsync($"/api/payment-methods/{method.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpStatusCode> PutProductAsync(Guid productId, Action<UpdateProductRequest> change)
    {
        var request = await InDbAsync(async db =>
        {
            var p = await db.Products.AsNoTracking().SingleAsync(x => x.Id == productId);
            return new UpdateProductRequest
            {
                Name = p.Name,
                ProductGroupId = p.ProductGroupId!.Value,
                UnitId = p.UnitId!.Value,
                Status = p.Status,
                PricingMode = p.PricingMode,
                TrackInventory = p.TrackInventory,
                PriceIncludesVat = p.PriceIncludesVat,
                DefaultTaxRate = p.DefaultTaxRate,
            };
        });
        change(request);
        return (await _client.PutAsJsonAsync($"/api/products/{productId}", request)).StatusCode;
    }
}
