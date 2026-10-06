using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherCreateTests : InventoryTestBase
{
    public StockVoucherCreateTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Create_stock_in_computes_totals_code_ledger_cost_price_and_activity()
    {
        var p1 = await CreateInventoryProductAsync("SV-P1");
        var p2 = await CreateInventoryProductAsync("SV-P2", PricingMode.PerCubicMeter);
        var p3 = await CreateInventoryProductAsync("SV-P3", track: false);
        var supplierId = await CreatePartnerAsync("NCC01", isCustomer: false, isSupplier: true);
        var voucherAt = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.FromHours(7));

        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-02 09:00",
            LineRequest(p1, 100, 50_000),
            new() { ProductId = p2, SheetCount = 10, Length = 2000, Width = 1000, Thickness = 50, UnitPrice = 1_500_000, DiscountRate = 5, VatRate = 8 },
            LineRequest(p3, 1, 300_000));
        request.VoucherAt = voucherAt;
        request.PartnerId = supplierId;
        request.Freight = 200_000;
        request.OrderDiscount = 100_000;
        request.Lines[0].VatRate = 10;
        request.Lines[2].VatRate = 8;

        var (status, voucher, error) = await PostVoucherAsync(_client, request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        voucher!.Code.Should().Be("PN00001");
        voucher.Should().BeEquivalentTo(new
        {
            GoodsAmount = 6_800_000m, LineDiscountTotal = 75_000m, DiscountTotal = 175_000m,
            VatTotal = 628_513m, Total = 7_453_513m, PaidAmount = 7_453_513m,
            PartnerName = "Đối tượng NCC01",
        });
        voucher.Lines.Select(l => l.Quantity).Should().Equal(100m, 1m, 1m);
        voucher.Lines.Select(l => l.NetAmount).Should().Equal(5_418_216m, 1_516_115m, 319_182m);
        voucher.Lines.Select(l => l.FreightAllocated).Should().Equal(155_642m, 44_358m, 0m);
        voucher.Lines.Select(l => l.UnitName).Should().Equal("Tấm", "m³", "Tấm");

        var stored = await InDbAsync(db => db.StockVouchers.AsNoTracking().SingleAsync(v => v.Id == voucher.Id));
        stored.VoucherAt.Should().Be(voucherAt);
        stored.VoucherAt.Offset.Should().Be(TimeSpan.Zero);

        var ledger = await InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .Where(r => r.SourceId == voucher.Id).OrderBy(r => r.LineSortOrder).ToListAsync());
        ledger.Select(r => r.InValue).Should().Equal(5_573_858m, 1_560_473m);
        (await InDbAsync(db => db.StockBalances.SingleAsync(b => b.ProductId == p1))).Quantity.Should().Be(100m);
        (await InDbAsync(db => db.StockBalances.SingleAsync(b => b.ProductId == p2))).Quantity.Should().Be(1.0m);

        var products = await InDbAsync(db => db.Products.AsNoTracking()
            .Where(p => p.Id == p1 || p.Id == p2 || p.Id == p3).ToListAsync());
        products.Single(p => p.Id == p1).CostPrice.Should().Be(50_000m);
        products.Single(p => p.Id == p2).CostPrice.Should().Be(1_500_000m);
        products.Single(p => p.Id == p3).CostPrice.Should().Be(300_000m);
        products.Should().OnlyContain(p => p.CostPriceUpdatedOn == voucherAt);

        var activities = await InDbAsync(db => db.StockVoucherActivities.Where(a => a.StockVoucherId == voucher.Id).ToListAsync());
        activities.Should().ContainSingle().Which.Action.Should().Be(StockVoucherActivityAction.Created);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Codes_are_sequential_per_type_and_reset_monthly_when_configured()
    {
        var service = await CreateInventoryProductAsync("SV-DV", track: false);
        var inReason = await ReasonIdAsync("NKH");
        var outReason = await ReasonIdAsync("XKH");

        async Task<string> CreateAsync(StockDirection type, string at)
        {
            var (status, voucher, error) = await PostVoucherAsync(_client,
                VoucherRequest(type, type == StockDirection.In ? inReason : outReason, at, LineRequest(service, 1, 10_000)));
            status.Should().Be(HttpStatusCode.OK, error?.Message);
            return voucher!.Code;
        }

        (await CreateAsync(StockDirection.In, "2026-10-01 08:00")).Should().Be("PN00001");
        (await CreateAsync(StockDirection.In, "2026-10-01 09:00")).Should().Be("PN00002");
        (await CreateAsync(StockDirection.Out, "2026-10-01 10:00")).Should().Be("PX00001");

        await SetStockInNumberingAsync("{KH}{NAM}{THANG}{STT}", NumberingResetPolicy.Monthly);
        (await CreateAsync(StockDirection.In, "2026-10-05 08:00")).Should().Be("PN20261000001");
        (await CreateAsync(StockDirection.In, "2026-10-20 08:00")).Should().Be("PN20261000002");
        (await CreateAsync(StockDirection.In, "2026-11-02 08:00")).Should().Be("PN20261100001");

        await SetStockInNumberingAsync("{KH}{NAM}{STT}", NumberingResetPolicy.Yearly);
        (await CreateAsync(StockDirection.In, "2026-12-30 09:00")).Should().Be("PN202600001");
        (await CreateAsync(StockDirection.In, "2027-01-01 00:30")).Should().Be("PN202700001");
    }

    [Fact]
    public async Task Older_voucher_does_not_overwrite_newer_cost_price()
    {
        var p = await CreateInventoryProductAsync("SV-CP");
        var reason = await ReasonIdAsync("NKH");

        (await PostVoucherAsync(_client, VoucherRequest(StockDirection.In, reason, "2026-10-05 08:00", LineRequest(p, 1, 50_000))))
            .Status.Should().Be(HttpStatusCode.OK);
        (await PostVoucherAsync(_client, VoucherRequest(StockDirection.In, reason, "2026-10-01 08:00", LineRequest(p, 1, 40_000))))
            .Status.Should().Be(HttpStatusCode.OK);

        var product = await InDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == p));
        product.CostPrice.Should().Be(50_000m);
        product.CostPriceUpdatedOn.Should().Be(Vn("2026-10-05 08:00"));
        await AssertInvariantsAsync();
    }

    private async Task SetStockInNumberingAsync(string pattern, NumberingResetPolicy policy)
    {
        var response = await _client.PutAsJsonAsync("/api/inventory/numbering/StockIn",
            new UpdateNumberingRequest { Prefix = "PN", Length = 5, ResetPolicy = policy, Pattern = pattern });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
