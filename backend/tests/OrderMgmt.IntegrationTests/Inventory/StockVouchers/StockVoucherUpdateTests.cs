using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherUpdateTests : InventoryTestBase
{
    public StockVoucherUpdateTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Owner_updates_voucher_and_ledger_is_replaced()
    {
        var p1 = await CreateInventoryProductAsync("UP01");
        var p2 = await CreateInventoryProductAsync("UP02");
        var p3 = await CreateInventoryProductAsync("UP03");
        var kho02 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00",
            LineRequest(p1, 10, 50_000), LineRequest(p2, 5, 20_000));

        // Move the voucher to KHO02, change line 1's quantity, drop line 2 and add a new line.
        var request = UpdateRequestFrom(voucher);
        request.WarehouseId = kho02;
        request.Lines.ForEach(l => l.WarehouseId = null);
        request.Lines[0].Quantity = 12;
        request.Lines.RemoveAt(1);
        request.Lines.Add(new UpsertStockVoucherLineRequest { SortOrder = 1, ProductId = p3, Quantity = 3, UnitPrice = 30_000 });

        var (status, updated, error) = await PutVoucherAsync(_client, voucher.Id, request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Code.Should().Be(voucher.Code);
        updated.Version.Should().NotBe(voucher.Version);
        updated.Total.Should().Be(690_000m);
        updated.Lines.Select(l => (l.ProductId, l.WarehouseId, l.Quantity)).Should().Equal((p1, kho02, 12m), (p3, kho02, 3m));
        updated.Lines[0].Id.Should().Be(voucher.Lines[0].Id, "an existing line is updated in place");

        var ledger = await LedgerOfAsync(voucher.Id);
        ledger.Select(r => (r.ProductId, r.WarehouseId, r.QtyIn, r.InValue)).Should().Equal(
            (p1, kho02, 12m, 600_000m), (p3, kho02, 3m, 90_000m));
        (await StockOfAsync(p1, DefaultWarehouseId)).Should().Be(0m);
        (await StockOfAsync(p2, DefaultWarehouseId)).Should().Be(0m);
        (await StockOfAsync(p1, kho02)).Should().Be(12m);
        (await StockOfAsync(p3, kho02)).Should().Be(3m);

        var removed = await InDbAsync(db => db.StockVoucherLines.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(l => l.Id == voucher.Lines[1].Id));
        removed.IsDeleted.Should().BeTrue();

        var activities = await ReadDataAsync<List<StockVoucherActivityDto>>(
            await _client.GetAsync($"/api/stock-vouchers/{voucher.Id}/activities"));
        activities.Select(a => (a.Action, a.Description)).Should().Equal(
            (StockVoucherActivityAction.Updated, "Cập nhật phiếu"), (StockVoucherActivityAction.Created, "Tạo phiếu"));
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Edit_rules()
    {
        var p = await CreateInventoryProductAsync("UE01");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));

        var warehouseUser = await CreateClientForRoleAsync("ue_kho", RoleCodes.Warehouse);
        var notOwner = UpdateRequestFrom(voucher);
        notOwner.Note = "Người khác sửa";
        (await PutVoucherAsync(warehouseUser, voucher.Id, notOwner)).Status.Should().Be(HttpStatusCode.Forbidden);

        var editAll = await CreateClientWithPermissionsAsync("ue_all", null,
            Permissions.StockIn.View, Permissions.StockIn.Edit, Permissions.StockIn.EditAll);
        var (status, updated, error) = await PutVoucherAsync(editAll, voucher.Id, notOwner);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Note.Should().Be("Người khác sửa");

        var noVersion = UpdateRequestFrom(updated);
        noVersion.Version = null;
        var (missingStatus, _, missingError) = await PutVoucherAsync(_client, voucher.Id, noVersion);
        missingStatus.Should().Be(HttpStatusCode.BadRequest);
        missingError!.Details.Should().ContainKey("version");

        await InDbAsync(async db =>
        {
            var stored = await db.StockVouchers.SingleAsync(v => v.Id == voucher.Id);
            stored.Status = StockVoucherStatus.Cancelled;
            await db.SaveChangesAsync();
        });
        var cancelled = await GetVoucherAsync(_client, voucher.Id);
        var (cancelledStatus, _, cancelledError) = await PutVoucherAsync(_client, voucher.Id, UpdateRequestFrom(cancelled));
        cancelledStatus.Should().Be(HttpStatusCode.Conflict);
        cancelledError!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Stale_version_returns_409_concurrency()
    {
        var p = await CreateInventoryProductAsync("UC01");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));

        var first = UpdateRequestFrom(voucher);
        first.Note = "Lần 1";
        (await PutVoucherAsync(_client, voucher.Id, first)).Status.Should().Be(HttpStatusCode.OK);

        var second = UpdateRequestFrom(voucher);
        second.Note = "Lần 2";
        var (status, _, error) = await PutVoucherAsync(_client, voucher.Id, second);

        status.Should().Be(HttpStatusCode.Conflict);
        error!.Code.Should().Be("CONCURRENCY");
        (await GetVoucherAsync(_client, voucher.Id)).Note.Should().Be("Lần 1");
    }

    [Fact]
    public async Task Line_only_edit_still_checks_the_version()
    {
        var p = await CreateInventoryProductAsync("UL01");
        var kho02 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));

        // Header totals stay the same: only the line warehouse changes.
        var first = UpdateRequestFrom(voucher);
        first.Lines[0].WarehouseId = kho02;
        var (status, updated, error) = await PutVoucherAsync(_client, voucher.Id, first);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Total.Should().Be(voucher.Total);
        updated.Version.Should().NotBe(voucher.Version);

        var second = UpdateRequestFrom(voucher);
        second.Lines[0].Note = "Chỉ sửa ghi chú dòng";
        var (staleStatus, _, staleError) = await PutVoucherAsync(_client, voucher.Id, second);

        staleStatus.Should().Be(HttpStatusCode.Conflict);
        staleError!.Code.Should().Be("CONCURRENCY");
        var stored = await GetVoucherAsync(_client, voucher.Id);
        stored.Lines[0].Note.Should().BeNull();
        stored.Lines[0].WarehouseId.Should().Be(kho02);
    }

    [Fact]
    public async Task Unchanged_line_with_a_deactivated_product_stays_editable()
    {
        var p1 = await CreateInventoryProductAsync("UD01");
        var p2 = await CreateInventoryProductAsync("UD02");
        var inactive = await CreateInventoryProductAsync("UD03");
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00",
            LineRequest(p1, 10, 50_000), LineRequest(p2, 5, 20_000));
        await InDbAsync(async db =>
        {
            foreach (var product in await db.Products.Where(x => x.Id == p1 || x.Id == inactive).ToListAsync())
                product.Status = ProductStatus.Inactive;
            await db.SaveChangesAsync();
        });

        var noteOnly = UpdateRequestFrom(voucher);
        noteOnly.Note = "Chỉ sửa ghi chú";
        var (status, updated, error) = await PutVoucherAsync(_client, voucher.Id, noteOnly);
        status.Should().Be(HttpStatusCode.OK, error?.Message);

        var replaced = UpdateRequestFrom(updated!);
        replaced.Lines[0].ProductId = inactive;
        var (replacedStatus, _, replacedError) = await PutVoucherAsync(_client, voucher.Id, replaced);
        replacedStatus.Should().Be(HttpStatusCode.BadRequest);
        replacedError!.Details.Should().ContainKey("lines[0].productId");
    }

    [Fact]
    public async Task Moving_an_inbound_after_the_outbound_respects_policy()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var p = await CreateInventoryProductAsync("UM01");
        var inbound = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 6, 70_000));

        var request = UpdateRequestFrom(inbound);
        request.VoucherAt = Vn("2026-10-08 08:00");
        var (status, _, error) = await PutVoucherAsync(_client, inbound.Id, request);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        error.Details.Should().ContainKey("UM01@KHO01");
        (await GetVoucherAsync(_client, inbound.Id)).Should().BeEquivalentTo(new { VoucherAt = Vn("2026-10-02 08:00"), inbound.Version });
        (await LedgerOfAsync(inbound.Id)).Single().PostedAt.Should().Be(Vn("2026-10-02 08:00"));
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(4m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Period_lock_applies_to_old_and_new_dates()
    {
        var p = await CreateInventoryProductAsync("UK01");
        var older = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var newer = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-10 08:00", LineRequest(p, 5, 50_000));
        await SetPeriodLockAsync(new DateOnly(2026, 10, 5));

        var intoLocked = UpdateRequestFrom(newer);
        intoLocked.VoucherAt = Vn("2026-10-05 23:30");
        var (status, _, error) = await PutVoucherAsync(_client, newer.Id, intoLocked);
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Code.Should().Be("PERIOD_LOCKED");

        var lockedVoucher = UpdateRequestFrom(older);
        lockedVoucher.Note = "Sửa phiếu đã khóa sổ";
        var (lockedStatus, _, lockedError) = await PutVoucherAsync(_client, older.Id, lockedVoucher);
        lockedStatus.Should().Be(HttpStatusCode.BadRequest);
        lockedError!.Code.Should().Be("PERIOD_LOCKED");

        var stillOpen = UpdateRequestFrom(newer);
        stillOpen.VoucherAt = Vn("2026-10-06 00:10");
        (await PutVoucherAsync(_client, newer.Id, stillOpen)).Status.Should().Be(HttpStatusCode.OK);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Back_dated_edit_recalculates_and_rejected_edit_changes_nothing()
    {
        var p = await CreateInventoryProductAsync("UB01");
        var inbound = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-01 08:00", LineRequest(p, 10, 100_000));
        var outbound = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-10 08:00", LineRequest(p, 5, 150_000));
        (await LedgerOfAsync(outbound.Id)).Single().CostAmount.Should().Be(500_000m);

        var price = UpdateRequestFrom(inbound);
        price.Lines[0].UnitPrice = 120_000;
        var (status, updated, error) = await PutVoucherAsync(_client, inbound.Id, price);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await LedgerOfAsync(outbound.Id)).Single().CostAmount.Should().Be(600_000m);

        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var quantity = UpdateRequestFrom(updated!);
        quantity.Lines[0].Quantity = 3;
        var (rejectedStatus, _, rejectedError) = await PutVoucherAsync(_client, inbound.Id, quantity);

        rejectedStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        rejectedError!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await GetVoucherAsync(_client, inbound.Id)).Lines.Single().Quantity.Should().Be(10m);
        (await LedgerOfAsync(inbound.Id)).Single().QtyIn.Should().Be(10m);
        (await LedgerOfAsync(outbound.Id)).Single().CostAmount.Should().Be(600_000m);
        await AssertInvariantsAsync();
    }
}
