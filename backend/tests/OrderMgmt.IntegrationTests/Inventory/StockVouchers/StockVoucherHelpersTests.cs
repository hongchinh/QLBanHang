using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherHelpersTests : InventoryTestBase
{
    public StockVoucherHelpersTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Defaults_return_next_code_and_last_used_selections()
    {
        var p = await CreateInventoryProductAsync("DF01");
        var inactiveFirst = await CreateWarehouseAsync(MainBranchId, "KHO00");
        await InDbAsync(async db =>
        {
            (await db.Warehouses.SingleAsync(w => w.Id == inactiveFirst)).IsActive = false;
            await db.SaveChangesAsync();
        });
        var kho02 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        (await _client.PostAsJsonAsync("/api/stock-reasons", new CreateStockReasonRequest
        {
            Code = "AAA", Name = "Nhập AAA", Direction = StockDirection.In, PartnerType = PartnerType.Any,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // No voucher yet: first active warehouse by code, first system reason by code, no payment method.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var fresh = await GetDefaultsAsync(_client, "type=In");
        fresh.Should().BeEquivalentTo(new
        {
            NextCode = "PN00001",
            WarehouseId = (Guid?)DefaultWarehouseId,
            ReasonId = (Guid?)await ReasonIdAsync("NKH"),
            PaymentMethodId = (Guid?)null,
        });
        fresh.VoucherAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow.AddSeconds(1));
        fresh.VoucherAt.Offset.Should().Be(TimeSpan.Zero);
        (await GetDefaultsAsync(_client, "type=Out")).Should().BeEquivalentTo(new
        {
            NextCode = "PX00001", ReasonId = (Guid?)await ReasonIdAsync("XBH"),
        });

        // The current user's latest voucher of the type gives the selections.
        var supplier = await CreatePartnerAsync("DF-NCC", isCustomer: false, isSupplier: true);
        var ck = await InDbAsync(db => db.PaymentMethods.Where(m => m.Code == "CK").Select(m => m.Id).SingleAsync());
        await CreateVoucherAsync(_client, StockDirection.In, "NMH", "2026-10-02 08:00", r =>
        {
            r.WarehouseId = kho02;
            r.PartnerId = supplier;
            r.PaymentMethodId = ck;
        }, LineRequest(p, 1, 10_000));

        (await GetDefaultsAsync(_client, "type=In")).Should().BeEquivalentTo(new
        {
            NextCode = "PN00002",
            WarehouseId = (Guid?)kho02,
            ReasonId = (Guid?)await ReasonIdAsync("NMH"),
            PaymentMethodId = (Guid?)ck,
        });
        (await GetDefaultsAsync(_client, "type=Out")).WarehouseId.Should().Be(DefaultWarehouseId);
        var warehouseUser = await CreateClientForRoleAsync("df_kho", RoleCodes.Warehouse);
        (await GetDefaultsAsync(warehouseUser, "type=In")).Should().BeEquivalentTo(new
        {
            NextCode = "PN00002", WarehouseId = (Guid?)DefaultWarehouseId, PaymentMethodId = (Guid?)null,
        });

        // Monthly numbering: the next code follows the requested VN date, and peeking does not use a number.
        await SetStockInNumberingAsync("{KH}{NAM}{THANG}{STT}", NumberingResetPolicy.Monthly);
        (await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 08:00", LineRequest(p, 1, 10_000)))
            .Code.Should().Be("PN20261000001");

        var october = await GetDefaultsAsync(_client, $"type=In&voucherAt={Uri.EscapeDataString("2026-10-20T08:00:00+07:00")}");
        october.NextCode.Should().Be("PN20261000002");
        october.VoucherAt.Should().Be(Vn("2026-10-20 08:00"));
        october.VoucherAt.Offset.Should().Be(TimeSpan.Zero);
        // 2026-10-31T17:30Z is already 2026-11-01 in Vietnam.
        (await GetDefaultsAsync(_client, "type=In&voucherAt=2026-10-31T17:30:00Z")).NextCode.Should().Be("PN20261100001");
        (await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-20 08:00", LineRequest(p, 1, 10_000)))
            .Code.Should().Be("PN20261000002");
    }

    [Fact]
    public async Task Defaults_follow_previous_voucher_date_mode()
    {
        await UpdateInventorySettingsAsync(s => s.DefaultDateMode = DefaultDateMode.PreviousVoucher);
        var p = await CreateInventoryProductAsync("DF02");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        (await GetDefaultsAsync(_client, "type=In")).VoucherAt.Should().BeOnOrAfter(before, "no previous voucher yet");

        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 1, 10_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-15 08:00", LineRequest(p, 1, 10_000));

        // The latest-created voucher decides, not the latest-dated one.
        var defaults = await GetDefaultsAsync(_client, "type=In");
        defaults.VoucherAt.Should().Be(Vn("2026-09-15 08:00"));
        defaults.VoucherAt.Offset.Should().Be(TimeSpan.Zero);
        defaults.NextCode.Should().Be("PN00003");

        (await GetDefaultsAsync(_client, "type=In&voucherAt=2026-10-10T01:00:00Z")).VoucherAt.Should().Be(Vn("2026-10-10 08:00"));
        (await GetDefaultsAsync(_client, "type=Out")).VoucherAt.Should().BeOnOrAfter(before, "stock-out vouchers have their own history");
    }

    [Fact]
    public async Task Stock_at_respects_time_and_excludes_own_voucher()
    {
        var p = await CreateInventoryProductAsync("SA01");
        var kho02 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        var outbound = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 3, 70_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-10 08:00", LineRequest(p, 5, 50_000));

        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var inB = CloneAdminClient();
        UseBranch(inB, branchB);
        await CreateVoucherAsync(inB, StockDirection.In, "NKH", "2026-10-02 08:00", r => r.WarehouseId = warehouseB,
            LineRequest(p, 7, 50_000));

        var all = await StockAtAsync("2026-10-05 08:00", null, (p, DefaultWarehouseId), (p, kho02), (p, warehouseB));
        all.Select(r => (r.ProductId, r.WarehouseId, r.Quantity)).Should().Equal(
            (p, DefaultWarehouseId, 7m), (p, kho02, 0m), (p, warehouseB, 0m));

        (await StockAtAsync("2026-10-05 07:59", null, (p, DefaultWarehouseId))).Single().Quantity.Should().Be(10m);
        (await StockAtAsync("2026-10-01 08:00", null, (p, DefaultWarehouseId))).Single().Quantity.Should().Be(0m);
        (await StockAtAsync("2026-10-10 08:00", null, (p, DefaultWarehouseId))).Single().Quantity.Should().Be(12m);
        (await StockAtAsync("2026-10-05 08:00", outbound.Id, (p, DefaultWarehouseId))).Single().Quantity.Should().Be(10m,
            "the edited voucher's own rows are excluded");
    }

    [Fact]
    public async Task Partner_search_scoping()
    {
        var customer = await CreatePartnerAsync("PS-KH", isCustomer: true, isSupplier: false);
        var supplier = await CreatePartnerAsync("PS-NCC", isCustomer: false, isSupplier: true);
        var both = await CreatePartnerAsync("PS-HH", isCustomer: true, isSupplier: true);
        var inactive = await CreatePartnerAsync("PS-NGUNG", isCustomer: false, isSupplier: true);
        await InDbAsync(async db =>
        {
            (await db.Customers.SingleAsync(c => c.Id == inactive)).Status = CustomerStatus.Inactive;
            await db.SaveChangesAsync();
        });
        var nmh = await ReasonIdAsync("NMH");
        var xbh = await ReasonIdAsync("XBH");

        async Task<List<Guid>> SearchAsync(HttpClient client, string query) =>
            (await ReadDataAsync<List<CustomerSearchItemDto>>(
                await client.GetAsync($"/api/stock-vouchers/partners?keyword=PS-&{query}"))).Select(x => x.Id).ToList();

        // With a reason: its partner type, active partners only.
        (await SearchAsync(_client, $"type=In&reasonId={nmh}")).Should().BeEquivalentTo(new[] { supplier, both });
        (await SearchAsync(_client, $"type=Out&reasonId={xbh}")).Should().BeEquivalentTo(new[] { customer, both });
        (await SearchAsync(_client, $"type=In&reasonId={await ReasonIdAsync("NKH")}"))
            .Should().BeEquivalentTo(new[] { customer, supplier, both });
        // List filter: any role, inactive partners included.
        (await SearchAsync(_client, "type=In")).Should().BeEquivalentTo(new[] { customer, supplier, both, inactive });

        (await _client.GetAsync($"/api/stock-vouchers/partners?type=Out&keyword=PS-&reasonId={nmh}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the reason must match the voucher type");

        var outViewer = await CreateClientWithPermissionsAsync("ps_out", null, Permissions.StockOut.View);
        (await SearchAsync(outViewer, $"type=Out&reasonId={xbh}")).Should().BeEquivalentTo(new[] { customer, both });
        (await outViewer.GetAsync("/api/stock-vouchers/partners?type=In&keyword=PS-"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// POST /stock-at with `at` sent as a VN-offset instant (the service normalizes it to UTC).
    private async Task<List<StockAtResult>> StockAtAsync(string at, Guid? excludeVoucherId,
        params (Guid ProductId, Guid WarehouseId)[] items) =>
        await ReadDataAsync<List<StockAtResult>>(await _client.PostAsJsonAsync("/api/stock-vouchers/stock-at",
            new StockAtRequest
            {
                Type = StockDirection.Out,
                At = Vn(at).ToOffset(VnTime.Offset),
                ExcludeVoucherId = excludeVoucherId,
                Items = items.Select(i => new StockAtItem { ProductId = i.ProductId, WarehouseId = i.WarehouseId }).ToList(),
            }));
}
