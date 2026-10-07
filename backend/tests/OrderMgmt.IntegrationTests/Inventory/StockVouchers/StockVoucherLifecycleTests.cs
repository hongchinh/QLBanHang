using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherLifecycleTests : InventoryTestBase
{
    public StockVoucherLifecycleTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Cancel_and_restore_move_ledger_rows_and_log_activities()
    {
        var p = await CreateInventoryProductAsync("LC01");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));

        var (status, cancelled, error) = await ActionAsync(_client, voucher.Id, "cancel", voucher.Version);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        cancelled!.Should().BeEquivalentTo(new
        {
            Status = StockVoucherStatus.Cancelled, CancelledBy = (Guid?)voucher.OwnerUserId, CanEdit = false, CanCancel = true,
        });
        cancelled.CancelledAt.Should().NotBeNull();
        (await LedgerOfAsync(voucher.Id)).Should().BeEmpty();
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(0m);
        (await ActionAsync(_client, voucher.Id, "cancel", cancelled.Version)).Status.Should().Be(HttpStatusCode.Conflict);

        var (restoreStatus, restored, restoreError) = await ActionAsync(_client, voucher.Id, "restore", cancelled.Version);

        restoreStatus.Should().Be(HttpStatusCode.OK, restoreError?.Message);
        restored!.Should().BeEquivalentTo(new
        {
            Status = StockVoucherStatus.Active, CancelledAt = (DateTimeOffset?)null, CancelledBy = (Guid?)null, CanEdit = true,
        });
        (await LedgerOfAsync(voucher.Id)).Select(r => (r.QtyIn, r.InValue)).Should().Equal((10m, 500_000m));
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(10m);
        (await ActionAsync(_client, voucher.Id, "restore", restored.Version)).Status.Should().Be(HttpStatusCode.Conflict);

        var activities = await ReadDataAsync<List<StockVoucherActivityDto>>(
            await _client.GetAsync($"/api/stock-vouchers/{voucher.Id}/activities"));
        activities.Select(a => (a.Action, a.Description)).Should().Equal(
            (StockVoucherActivityAction.Restored, "Khôi phục phiếu"),
            (StockVoucherActivityAction.Cancelled, "Hủy phiếu"),
            (StockVoucherActivityAction.Created, "Tạo phiếu"));
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Lifecycle_permissions_and_period_lock()
    {
        var p = await CreateInventoryProductAsync("LP01");
        var noCancel = await CreateClientWithPermissionsAsync("lp_edit", null,
            Permissions.StockIn.View, Permissions.StockIn.Create, Permissions.StockIn.Edit);
        var own = await CreateVoucherAsync(noCancel, StockDirection.In, "NKH", "2026-10-06 08:00", LineRequest(p, 1, 10_000));
        (await ActionAsync(noCancel, own.Id, "cancel", own.Version)).Status.Should().Be(HttpStatusCode.Forbidden);

        // Not the owner and no edit_all.
        var adminVoucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-06 08:00", LineRequest(p, 1, 10_000));
        var warehouseUser = await CreateClientForRoleAsync("lp_kho", RoleCodes.Warehouse);
        (await ActionAsync(warehouseUser, adminVoucher.Id, "cancel", adminVoucher.Version)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await DeleteVoucherAsync(warehouseUser, adminVoucher.Id, adminVoucher.Version)).Status.Should().Be(HttpStatusCode.Forbidden);

        var (missingStatus, _, missingError) = await ActionAsync(_client, adminVoucher.Id, "cancel", null);
        missingStatus.Should().Be(HttpStatusCode.BadRequest);
        missingError!.Details.Should().ContainKey("version");

        var toCancel = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 1, 10_000));
        var toDelete = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00", LineRequest(p, 1, 10_000));
        var toRestore = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-04 08:00", LineRequest(p, 1, 10_000));
        var (_, cancelledBeforeLock, _) = await ActionAsync(_client, toRestore.Id, "cancel", toRestore.Version);
        await SetPeriodLockAsync(new DateOnly(2026, 10, 5));

        (await ActionAsync(_client, toCancel.Id, "cancel", toCancel.Version)).Error!.Code.Should().Be("PERIOD_LOCKED");
        (await ActionAsync(_client, toRestore.Id, "restore", cancelledBeforeLock!.Version)).Error!.Code.Should().Be("PERIOD_LOCKED");
        var (deleteStatus, deleteError) = await DeleteVoucherAsync(_client, toDelete.Id, toDelete.Version);
        deleteStatus.Should().Be(HttpStatusCode.BadRequest);
        deleteError!.Code.Should().Be("PERIOD_LOCKED");
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Policy_applies_to_cancel_and_restore()
    {
        var p = await CreateInventoryProductAsync("LN01");
        var inbound = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var outbound = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 6, 70_000));

        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var (blockedStatus, _, blockedError) = await ActionAsync(_client, inbound.Id, "cancel", inbound.Version, acknowledge: true);
        blockedStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        blockedError!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        blockedError.Details.Should().ContainKey("LN01@KHO01");
        (await GetVoucherAsync(_client, inbound.Id)).Status.Should().Be(StockVoucherStatus.Active);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(4m);

        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Warn);
        (await ActionAsync(_client, inbound.Id, "cancel", inbound.Version)).Error!.Code.Should().Be("NEGATIVE_STOCK_WARNING");
        var (ackStatus, _, ackError) = await ActionAsync(_client, inbound.Id, "cancel", inbound.Version, acknowledge: true);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(-6m);

        // Cancelling the outbound only removes the deficit, so it needs no acknowledgement (D31).
        var (outStatus, cancelledOutbound, outError) = await ActionAsync(_client, outbound.Id, "cancel", outbound.Version);
        outStatus.Should().Be(HttpStatusCode.OK, outError?.Message);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(0m);

        var (restoreStatus, _, restoreError) = await ActionAsync(_client, outbound.Id, "restore", cancelledOutbound!.Version);
        restoreStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        restoreError!.Code.Should().Be("NEGATIVE_STOCK_WARNING");
        (await GetVoucherAsync(_client, outbound.Id)).Status.Should().Be(StockVoucherStatus.Cancelled);
        (await ActionAsync(_client, outbound.Id, "restore", cancelledOutbound.Version, acknowledge: true))
            .Status.Should().Be(HttpStatusCode.OK);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(-6m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Delete_soft_deletes_and_cancelled_cannot_be_deleted()
    {
        var p = await CreateInventoryProductAsync("LD01");
        var warehouseUser = await CreateClientForRoleAsync("ld_kho", RoleCodes.Warehouse);
        var first = await CreateVoucherAsync(warehouseUser, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var second = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-03 08:00", LineRequest(p, 4, 50_000));

        // The owner deletes their own voucher with the delete permission.
        var (status, error) = await DeleteVoucherAsync(warehouseUser, first.Id, first.Version);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await _client.GetAsync($"/api/stock-vouchers/{first.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LedgerOfAsync(first.Id)).Should().BeEmpty();
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(4m);
        var stored = await InDbAsync(db => db.StockVouchers.IgnoreQueryFilters().AsNoTracking()
            .Include(v => v.Lines).SingleAsync(v => v.Id == first.Id));
        stored.IsDeleted.Should().BeTrue();
        stored.Lines.Should().OnlyContain(l => l.IsDeleted);
        var list = await ReadDataAsync<StockVoucherListResult>(await _client.GetAsync("/api/stock-vouchers?type=In"));
        list.Items.Select(i => i.Id).Should().Equal(second.Id);
        list.Items.Single().OwnerName.Should().Be("Quản trị hệ thống");
        var owners = await ReadDataAsync<List<StockVoucherOwnerDto>>(await _client.GetAsync("/api/stock-vouchers/owners?type=In"));
        owners.Select(o => o.Id).Should().Equal(second.OwnerUserId);

        var (_, cancelled, _) = await ActionAsync(_client, second.Id, "cancel", second.Version);
        var (cancelledStatus, cancelledError) = await DeleteVoucherAsync(_client, second.Id, cancelled!.Version);
        cancelledStatus.Should().Be(HttpStatusCode.Conflict);
        cancelledError!.Code.Should().Be("CONFLICT");
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Delete_inbound_feeding_outbound_respects_policy()
    {
        var p = await CreateInventoryProductAsync("LX01");
        var inbound = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 6, 70_000));

        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var (blockedStatus, blockedError) = await DeleteVoucherAsync(_client, inbound.Id, inbound.Version, acknowledge: true);
        blockedStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        blockedError!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await GetVoucherAsync(_client, inbound.Id)).Version.Should().Be(inbound.Version);
        (await LedgerOfAsync(inbound.Id)).Should().ContainSingle();
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(4m);

        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Warn);
        (await DeleteVoucherAsync(_client, inbound.Id, inbound.Version)).Error!.Code.Should().Be("NEGATIVE_STOCK_WARNING");
        var (ackStatus, ackError) = await DeleteVoucherAsync(_client, inbound.Id, inbound.Version, acknowledge: true);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        (await _client.GetAsync($"/api/stock-vouchers/{inbound.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(-6m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Cost_price_follows_the_latest_active_stock_in()
    {
        var p = await CreateInventoryProductAsync("LCP01");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(p, 1, 40_000));
        var newer = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 08:00", LineRequest(p, 1, 50_000));
        await ExpectCostAsync(p, 50_000m, Vn("2026-10-05 08:00"));

        var (_, cancelled, _) = await ActionAsync(_client, newer.Id, "cancel", newer.Version);
        await ExpectCostAsync(p, 40_000m, Vn("2026-10-01 08:00"));

        (await ActionAsync(_client, newer.Id, "restore", cancelled!.Version)).Status.Should().Be(HttpStatusCode.OK);
        await ExpectCostAsync(p, 50_000m, Vn("2026-10-05 08:00"));

        // A mistyped future date can be corrected: the stamp follows the latest voucher again.
        var mistyped = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2027-10-05 08:00", LineRequest(p, 1, 60_000));
        await ExpectCostAsync(p, 60_000m, Vn("2027-10-05 08:00"));
        var corrected = UpdateRequestFrom(mistyped);
        corrected.VoucherAt = Vn("2026-10-03 08:00");
        (await PutVoucherAsync(_client, mistyped.Id, corrected)).Status.Should().Be(HttpStatusCode.OK);
        await ExpectCostAsync(p, 50_000m, Vn("2026-10-05 08:00"));

        // No active stock-in left: the price stays and the stamp is cleared.
        var q = await CreateInventoryProductAsync("LCP02");
        var only = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(q, 1, 70_000));
        (await DeleteVoucherAsync(_client, only.Id, only.Version)).Status.Should().Be(HttpStatusCode.OK);
        await ExpectCostAsync(q, 70_000m, null);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Restore_reposts_the_stored_lines_unchanged()
    {
        var p = await CreateInventoryProductAsync("LR01");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var (_, cancelled, _) = await ActionAsync(_client, voucher.Id, "cancel", voucher.Version);
        await InDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync(x => x.Id == p);
            product.Status = ProductStatus.Inactive;
            product.Name = "Tên mới";
            await db.SaveChangesAsync();
        });

        var (status, restored, error) = await ActionAsync(_client, voucher.Id, "restore", cancelled!.Version);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        restored!.Lines.Should().BeEquivalentTo(voucher.Lines);
        restored.Lines.Single().ProductName.Should().Be("Hàng LR01");
        (await LedgerOfAsync(voucher.Id)).Select(r => (r.QtyIn, r.InValue)).Should().Equal((10m, 500_000m));
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(10m);
        await AssertInvariantsAsync();
    }

    private static async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, ApiError? Error)> ActionAsync(
        HttpClient client, Guid id, string action, uint? version, bool acknowledge = false) =>
        await ReadVoucherResponseAsync(await client.PostAsJsonAsync($"/api/stock-vouchers/{id}/{action}",
            new StockVoucherActionRequest { Version = version, AcknowledgeNegativeStock = acknowledge }));

    private static async Task<(HttpStatusCode Status, ApiError? Error)> DeleteVoucherAsync(
        HttpClient client, Guid id, uint version, bool acknowledge = false)
    {
        var (status, _, error) = await ReadVoucherResponseAsync(await client.DeleteAsync(
            $"/api/stock-vouchers/{id}?version={version}&acknowledgeNegativeStock={(acknowledge ? "true" : "false")}"));
        return (status, error);
    }

    private async Task ExpectCostAsync(Guid productId, decimal costPrice, DateTimeOffset? updatedOn)
    {
        var product = await InDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == productId));
        product.CostPrice.Should().Be(costPrice);
        product.CostPriceUpdatedOn.Should().Be(updatedOn);
    }
}
