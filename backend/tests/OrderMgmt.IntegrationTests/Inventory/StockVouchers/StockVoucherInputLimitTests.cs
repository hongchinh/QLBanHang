using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

/// Column limits, line caps, enum query values and inactive partners (second code review).
[Collection(nameof(PostgresCollection))]
public class StockVoucherInputLimitTests : InventoryTestBase
{
    public StockVoucherInputLimitTests(PostgresFixture pg) : base(pg) { }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
    }

    [Fact]
    public async Task Numbers_beyond_their_column_scale_or_range_are_rejected()
    {
        var p = await CreateInventoryProductAsync("LIM01");
        var line = LineRequest(p, 1.1234567m, 1_000.555m);
        line.VatRate = 8.125m;
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", line);
        request.Freight = 100_000_000_000_000_000m;
        request.PaidAmount = -1m;

        var (status, _, error) = await PostVoucherAsync(_client, request);

        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKeys("lines[0].quantity", "lines[0].unitPrice", "lines[0].vatRate", "freight", "paidAmount");
    }

    [Fact]
    public async Task Amount_too_large_for_the_column_is_rejected_on_its_line()
    {
        var p = await CreateInventoryProductAsync("LIM02");
        var (status, _, error) = await PostVoucherAsync(_client, VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"),
            "2026-10-02 08:00", LineRequest(p, 999_999_999_999m, 1_000_000_000m)));

        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("lines[0].unitPrice");
    }

    [Fact]
    public async Task Line_and_stock_at_counts_are_capped_and_type_must_be_defined()
    {
        var p = await CreateInventoryProductAsync("LIM03");
        var lines = Enumerable.Range(0, 501).Select(_ => LineRequest(p, 1, 1_000)).ToArray();
        var (status, _, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), "2026-10-02 08:00", lines));
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("lines");

        var stockAt = new StockAtRequest
        {
            Type = StockDirection.In,
            At = Vn("2026-10-02 08:00"),
            Items = Enumerable.Range(0, 501).Select(_ => new StockAtItem { ProductId = p, WarehouseId = DefaultWarehouseId }).ToList(),
        };
        (await ReadErrorAsync(await _client.PostAsJsonAsync("/api/stock-vouchers/stock-at", stockAt))).Details
            .Should().ContainKey("items");

        // MVC model binding rejects undefined enum values in the query string.
        foreach (var path in new[] { "owners?type=9", "defaults?type=9", "partners?type=9" })
            (await _client.GetAsync($"/api/stock-vouchers/{path}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inactive_partner_is_rejected_only_when_newly_chosen()
    {
        var p = await CreateInventoryProductAsync("LIM04");
        var partner = await CreatePartnerAsync("NCC-LIM", isCustomer: false, isSupplier: true);
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NMH", "2026-10-02 08:00",
            r => r.PartnerId = partner, LineRequest(p, 1, 1_000));
        await InDbAsync(async db =>
        {
            var stored = await db.Customers.SingleAsync(c => c.Id == partner);
            stored.Status = CustomerStatus.Inactive;
            await db.SaveChangesAsync();
        });

        var edit = UpdateRequestFrom(voucher);
        edit.Note = "Vẫn sửa được";
        (await PutVoucherAsync(_client, voucher.Id, edit)).Status.Should().Be(HttpStatusCode.OK);

        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-03 08:00", LineRequest(p, 1, 1_000));
        request.PartnerId = partner;
        var (status, _, error) = await PostVoucherAsync(_client, request);
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("partnerId");
    }
}
