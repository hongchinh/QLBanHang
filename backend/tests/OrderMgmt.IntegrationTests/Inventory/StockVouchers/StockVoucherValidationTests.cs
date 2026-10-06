using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherValidationTests : InventoryTestBase
{
    public StockVoucherValidationTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Reason_and_partner_rules()
    {
        var p = await CreateInventoryProductAsync("VL01", track: false);
        var customerOnly = await CreatePartnerAsync("KH01", isCustomer: true, isSupplier: false);
        var supplierOnly = await CreatePartnerAsync("NCC01", isCustomer: false, isSupplier: true);
        var nmh = await ReasonIdAsync("NMH");
        var xbh = await ReasonIdAsync("XBH");
        var noPartnerReason = (await ReadDataAsync<StockReasonDto>(await _client.PostAsJsonAsync("/api/stock-reasons",
            new CreateStockReasonRequest { Code = "NNB", Name = "Nhập nội bộ", Direction = StockDirection.In, PartnerType = PartnerType.None }))).Id;

        await ExpectErrorAsync(Voucher(StockDirection.In, await ReasonIdAsync("XKH"), p), "reasonId");
        await ExpectErrorAsync(Voucher(StockDirection.In, nmh, p, customerOnly), "partnerId");
        await ExpectErrorAsync(Voucher(StockDirection.In, nmh, p), "partnerId");
        await ExpectErrorAsync(Voucher(StockDirection.Out, xbh, p, supplierOnly), "partnerId");
        await ExpectErrorAsync(Voucher(StockDirection.In, noPartnerReason, p, supplierOnly), "partnerId");

        (await PostVoucherAsync(_client, Voucher(StockDirection.In, nmh, p, supplierOnly))).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Line_rules()
    {
        var unit = await CreateInventoryProductAsync("VL02");
        var area = await CreateInventoryProductAsync("VL03", PricingMode.PerSquareMeter);
        var reason = await ReasonIdAsync("NKH");
        var otherBranch = await CreateBranchAsync("CN02");
        var otherBranchWarehouse = await CreateWarehouseAsync(otherBranch, "KHO-B");

        var foreignWarehouse = Voucher(StockDirection.In, reason, unit);
        foreignWarehouse.Lines[0].WarehouseId = otherBranchWarehouse;
        await ExpectErrorAsync(foreignWarehouse, "lines[0].warehouseId");

        await ExpectErrorAsync(VoucherRequest(StockDirection.In, reason, "2026-10-02 08:00",
            new UpsertStockVoucherLineRequest { ProductId = area, SheetCount = 2, Length = 2000, UnitPrice = 10_000 }), "lines[0].width");

        await ExpectErrorAsync(VoucherRequest(StockDirection.In, reason, "2026-10-02 08:00", LineRequest(unit, 0, 10_000)), "lines[0].quantity");

        var orderDiscount = Voucher(StockDirection.In, reason, unit);
        orderDiscount.OrderDiscount = 10_001;
        await ExpectErrorAsync(orderDiscount, "orderDiscount");

        var manualDiscount = Voucher(StockDirection.In, reason, unit);
        manualDiscount.Lines[0].DiscountManual = true;
        manualDiscount.Lines[0].DiscountAmount = 10_001;
        await ExpectErrorAsync(manualDiscount, "lines[0].discountAmount");

        var emptyReason = Voucher(StockDirection.In, Guid.Empty, unit);
        await ExpectErrorAsync(emptyReason, "reasonId");
    }

    [Fact]
    public async Task Period_lock_boundary_uses_vn_date()
    {
        var p = await CreateInventoryProductAsync("VL04");
        var reason = await ReasonIdAsync("NKH");
        (await _client.PutAsJsonAsync($"/api/branches/{MainBranchId}/lock",
            new SetPeriodLockRequest { LockedUntil = new DateOnly(2026, 10, 5) })).StatusCode.Should().Be(HttpStatusCode.OK);

        var (status, _, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.In, reason, "2026-10-05 23:30", LineRequest(p, 1, 10_000)));
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Code.Should().Be("PERIOD_LOCKED");

        (await PostVoucherAsync(_client, VoucherRequest(StockDirection.In, reason, "2026-10-06 00:10", LineRequest(p, 1, 10_000))))
            .Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Type_permission_and_working_branch()
    {
        var p = await CreateInventoryProductAsync("VL05");
        var limited = await CreateClientWithPermissionsAsync("kho_in", null, "stock_in.view", "stock_in.create");

        (await PostVoucherAsync(limited, VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-02 08:00", LineRequest(p, 1, 10_000))))
            .Status.Should().Be(HttpStatusCode.Forbidden);
        (await PostVoucherAsync(limited, VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", LineRequest(p, 1, 10_000))))
            .Status.Should().Be(HttpStatusCode.OK);

        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var admin = CloneAdminClient();
        UseBranch(admin, branchB);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", LineRequest(p, 1, 10_000));
        request.WarehouseId = warehouseB;

        var (status, voucher, error) = await PostVoucherAsync(admin, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        voucher!.Code.Should().Be("PN00001");
        (await InDbAsync(db => db.StockVouchers.AsNoTracking().SingleAsync(v => v.Id == voucher.Id))).BranchId.Should().Be(branchB);
    }

    private UpsertStockVoucherRequest Voucher(StockDirection type, Guid reasonId, Guid productId, Guid? partnerId = null)
    {
        var request = VoucherRequest(type, reasonId, "2026-10-02 08:00", LineRequest(productId, 1, 10_000));
        request.PartnerId = partnerId;
        return request;
    }

    private async Task ExpectErrorAsync(UpsertStockVoucherRequest request, string key)
    {
        var (status, _, error) = await PostVoucherAsync(_client, request);
        status.Should().Be(HttpStatusCode.BadRequest, $"expected a 400 with key {key}");
        error!.Details.Should().ContainKey(key);
    }
}
