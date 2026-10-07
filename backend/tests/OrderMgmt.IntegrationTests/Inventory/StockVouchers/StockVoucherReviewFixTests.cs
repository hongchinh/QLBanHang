using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

/// Regression tests for the Phase 04–05 review findings.
[Collection(nameof(PostgresCollection))]
public class StockVoucherReviewFixTests : InventoryTestBase
{
    public StockVoucherReviewFixTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Receipt_after_an_existing_deficit_passes_under_block()
    {
        var p = await CreateInventoryProductAsync("RF01");
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Allow);
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 10, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-08 08:00", LineRequest(p, 2, 20_000));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);

        // (a) dated after every row: the window has no old rows, the deficit only shrinks.
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-09 08:00", LineRequest(p, 4, 40_000));
        // (b) dated before a later row: the first negative point (10-05) does not move.
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-07 08:00", LineRequest(p, 3, 30_000));

        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(-1m);
        var deeper = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-06 08:00", LineRequest(p, 1, 0)));
        deeper.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Duplicate_line_ids_and_missing_voucher_date_are_rejected()
    {
        var p = await CreateInventoryProductAsync("RF02");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 5, 100_000));

        var request = UpdateRequestFrom(voucher);
        var copy = UpdateRequestFrom(voucher).Lines[0];
        copy.Quantity = 7;
        copy.SortOrder = 1;
        request.Lines.Add(copy);
        var (status, _, error) = await PutVoucherAsync(_client, voucher.Id, request);
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("lines[1].id");

        var undated = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", LineRequest(p, 1, 1_000));
        undated.VoucherAt = default;
        var (undatedStatus, _, undatedError) = await PostVoucherAsync(_client, undated);
        undatedStatus.Should().Be(HttpStatusCode.BadRequest);
        undatedError!.Details.Should().ContainKey("voucherAt");

        var farFuture = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", LineRequest(p, 1, 1_000));
        farFuture.VoucherAt = new DateTimeOffset(9999, 11, 1, 0, 0, 0, TimeSpan.Zero);
        (await PostVoucherAsync(_client, farFuture)).Error!.Details.Should().ContainKey("voucherAt");
    }

    [Fact]
    public async Task Create_only_user_gets_the_saved_voucher_back()
    {
        var p = await CreateInventoryProductAsync("RF03");
        var createOnly = await CreateClientWithPermissionsAsync("kho_create", null, "stock_in.create");

        var (status, voucher, error) = await PostVoucherAsync(createOnly,
            VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", LineRequest(p, 1, 1_000)));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        voucher!.Code.Should().Be("PN00001");
        (await createOnly.GetAsync($"/api/stock-vouchers/{voucher.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Restore_is_rejected_when_the_product_stock_unit_changed()
    {
        var p = await CreateInventoryProductAsync("RF04");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var cancelled = await ReadVoucherResponseAsync(await _client.PostAsJsonAsync(
            $"/api/stock-vouchers/{voucher.Id}/cancel", new StockVoucherActionRequest { Version = voucher.Version }));
        await InDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync(x => x.Id == p);
            product.PricingMode = PricingMode.PerSquareMeter;
            await db.SaveChangesAsync();
        });

        var (status, _, error) = await ReadVoucherResponseAsync(await _client.PostAsJsonAsync(
            $"/api/stock-vouchers/{voucher.Id}/restore", new StockVoucherActionRequest { Version = cancelled.Voucher!.Version }));

        status.Should().Be(HttpStatusCode.Conflict);
        error!.Message.Should().Contain("RF04");
        (await LedgerOfAsync(voucher.Id)).Should().BeEmpty();
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warehouse_of_another_branch_cannot_be_read_or_changed_without_access_all()
    {
        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var manager = await CreateClientWithPermissionsAsync("kho_mgr", null, "inventory.catalogs.manage");

        (await manager.GetAsync($"/api/warehouses/{warehouseB}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.PutAsJsonAsync($"/api/warehouses/{warehouseB}",
            new UpdateWarehouseRequest { Name = "x", BranchId = MainBranchId, IsActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.DeleteAsync($"/api/warehouses/{warehouseB}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var stored = await InDbAsync(db => db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == warehouseB));
        stored.Should().BeEquivalentTo(new { BranchId = branchB, Name = "Kho KHO-B", IsActive = true, IsDeleted = false });
    }
}
