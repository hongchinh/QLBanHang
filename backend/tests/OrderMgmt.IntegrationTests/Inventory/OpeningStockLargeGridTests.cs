using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// A whole-catalog opening stock grid must fit in PostgreSQL's shared lock table
/// (max_locks_per_transaction × max_connections): one advisory lock per product (review finding).
[Collection(nameof(PostgresCollection))]
public class OpeningStockLargeGridTests : InventoryEngineTestBase
{
    private const int ProductCount = 1_500;

    public OpeningStockLargeGridTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Posting_locks_take_one_advisory_lock_per_product()
    {
        var productIds = Enumerable.Range(0, ProductCount).Select(_ => Guid.NewGuid()).ToList();

        var held = await InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IInventoryPostingService>().AcquireLocksAsync(MainBranchId, productIds, ct);
            return await sp.GetRequiredService<AppDbContext>().Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()")
                .SingleAsync(ct);
        });

        held.Should().Be(ProductCount + 1, "the shared branch gate plus one key per product");
    }

    [Fact]
    public async Task Saves_an_opening_stock_grid_of_many_products()
    {
        var productIds = await CreateProductsAsync(ProductCount);
        var request = new SaveOpeningStockRequest
        {
            WarehouseId = DefaultWarehouseId,
            OpeningDate = new DateOnly(2026, 10, 1),
            Lines = productIds.Select(id => new SaveOpeningStockLine { ProductId = id, Quantity = 2, Amount = 200_000 }).ToList(),
        };

        var response = await _client.PutAsJsonAsync("/api/inventory/opening-stock", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await InDbAsync(db => db.InventoryLedger.CountAsync(e => e.SourceId == DefaultWarehouseId)))
            .Should().Be(ProductCount);
        (await InDbAsync(db => db.StockBalances.CountAsync(b => b.WarehouseId == DefaultWarehouseId && b.Quantity == 2)))
            .Should().Be(ProductCount);
    }

    /// Tracked products inserted in one SaveChanges (the API would be far slower).
    private Task<List<Guid>> CreateProductsAsync(int count) =>
        InDbAsync(async db =>
        {
            var groupId = await db.ProductGroups.Where(g => g.Code == "EPS").Select(g => g.Id).SingleAsync();
            var products = Enumerable.Range(1, count).Select(i => new Product
            {
                Code = $"LG{i:D5}",
                Name = $"Hàng LG{i:D5}",
                ProductGroupId = groupId,
                UnitId = _unitId,
                PricingMode = PricingMode.PerUnit,
                TrackInventory = true,
                Status = ProductStatus.Active,
            }).ToList();
            db.Products.AddRange(products);
            await db.SaveChangesAsync();
            return products.Select(p => p.Id).ToList();
        });
}
