using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherConcurrencyTests : InventoryTestBase
{
    public StockVoucherConcurrencyTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Parallel_creates_get_distinct_sequential_codes()
    {
        var p = await CreateInventoryProductAsync("CC01");
        var reason = await ReasonIdAsync("NKH");
        var clients = Enumerable.Range(0, 5).Select(_ => CloneAdminClient()).ToList();

        var results = await Task.WhenAll(clients.Select(c =>
            PostVoucherAsync(c, VoucherRequest(StockDirection.In, reason, "2026-10-02 08:00", LineRequest(p, 1, 10_000)))));

        results.Should().OnlyContain(r => r.Status == HttpStatusCode.OK, string.Join("; ", results.Select(r => r.Error?.Message)));
        results.Select(r => r.Voucher!.Code).Should().BeEquivalentTo("PN00001", "PN00002", "PN00003", "PN00004", "PN00005");
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(5m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Parallel_outbounds_cannot_overdraw_under_block_policy()
    {
        var p = await CreateInventoryProductAsync("CC02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 50_000));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var reason = await ReasonIdAsync("XKH");

        var results = await Task.WhenAll(new[] { CloneAdminClient(), CloneAdminClient() }.Select(c =>
            PostVoucherAsync(c, VoucherRequest(StockDirection.Out, reason, "2026-10-05 08:00", LineRequest(p, 6, 70_000)))));

        results.Select(r => r.Status).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity });
        results.Single(r => r.Status == HttpStatusCode.UnprocessableEntity).Error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        results.Single(r => r.Status == HttpStatusCode.OK).Voucher!.Code.Should().Be("PX00001");
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(4m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Rejected_create_leaves_no_voucher_ledger_or_counter_change()
    {
        var p = await CreateInventoryProductAsync("CC03");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 2, 50_000));
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var ledgerBefore = await InDbAsync(db => db.InventoryLedger.AsNoTracking().CountAsync());

        var (status, _, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00", LineRequest(p, 3, 70_000)));

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await InDbAsync(db => db.StockVouchers.IgnoreQueryFilters().CountAsync(v => v.Type == StockDirection.Out))).Should().Be(0);
        (await InDbAsync(db => db.InventoryLedger.AsNoTracking().CountAsync())).Should().Be(ledgerBefore);
        (await StockOfAsync(p, DefaultWarehouseId)).Should().Be(2m);
        (await GetDefaultsAsync(_client, "type=Out")).NextCode.Should().Be("PX00001");
        await AssertInvariantsAsync();
    }
}
