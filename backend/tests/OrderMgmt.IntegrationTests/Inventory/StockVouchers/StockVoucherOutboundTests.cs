using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherOutboundTests : InventoryTestBase
{
    public StockVoucherOutboundTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Stock_out_ledger_rows_carry_period_average_cost()
    {
        var p = await CreateInventoryProductAsync("OUT01");
        await StockInAsync(p, 100, 50_000, "2026-10-02 08:00");

        var (status, voucher, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00", LineRequest(p, 30, 70_000)));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        voucher!.Code.Should().Be("PX00001");
        var row = await InDbAsync(db => db.InventoryLedger.AsNoTracking().SingleAsync(r => r.SourceId == voucher.Id));
        row.QtyOut.Should().Be(30m);
        row.UnitCost.Should().Be(50_000m);
        row.CostAmount.Should().Be(1_500_000m);
        (await InDbAsync(db => db.StockBalances.SingleAsync(b => b.ProductId == p))).Quantity.Should().Be(70m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warn_policy_returns_422_warning_then_succeeds_with_acknowledgement()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Warn);
        var p = await CreateInventoryProductAsync("OUT02");
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00", LineRequest(p, 5, 70_000));

        var (status, _, error) = await PostVoucherAsync(_client, request);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("NEGATIVE_STOCK_WARNING");
        error.Details.Should().ContainKey("OUT02@KHO01");
        (await InDbAsync(db => db.StockVouchers.CountAsync())).Should().Be(0);

        request.AcknowledgeNegativeStock = true;
        var (ackStatus, voucher, ackError) = await PostVoucherAsync(_client, request);
        ackStatus.Should().Be(HttpStatusCode.OK, ackError?.Message);
        voucher!.Code.Should().Be("PX00001");
        (await InDbAsync(db => db.StockBalances.SingleAsync(b => b.ProductId == p))).Quantity.Should().Be(-5m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Block_policy_returns_422_blocked_even_with_acknowledgement()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var p = await CreateInventoryProductAsync("OUT03");
        var request = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00", LineRequest(p, 5, 70_000));
        request.AcknowledgeNegativeStock = true;

        var (status, _, error) = await PostVoucherAsync(_client, request);

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        error.Details.Should().ContainKey("OUT03@KHO01");
        (await InDbAsync(db => db.StockVouchers.CountAsync())).Should().Be(0);
        (await InDbAsync(db => db.InventoryLedger.CountAsync())).Should().Be(0);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Untracked_lines_skip_ledger_and_stock_check()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);
        var service = await CreateInventoryProductAsync("OUT-DV", track: false);

        var (status, voucher, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00", LineRequest(service, 3, 100_000)));

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await InDbAsync(db => db.InventoryLedger.CountAsync(r => r.SourceId == voucher!.Id))).Should().Be(0);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Lines_of_the_same_product_are_checked_cumulatively()
    {
        var p = await CreateInventoryProductAsync("OUT04");
        await StockInAsync(p, 10, 50_000, "2026-10-02 08:00");
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Block);

        var (status, _, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.Out, await ReasonIdAsync("XKH"), "2026-10-05 08:00",
                LineRequest(p, 6, 70_000), LineRequest(p, 6, 70_000)));

        status.Should().Be(HttpStatusCode.UnprocessableEntity);
        error!.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        error.Details.Should().ContainKey("OUT04@KHO01");
        (await InDbAsync(db => db.StockBalances.SingleAsync(b => b.ProductId == p))).Quantity.Should().Be(10m);
        await AssertInvariantsAsync();
    }

    private async Task StockInAsync(Guid productId, decimal quantity, decimal unitPrice, string at)
    {
        var (status, _, error) = await PostVoucherAsync(_client,
            VoucherRequest(StockDirection.In, await ReasonIdAsync("NKH"), at, LineRequest(productId, quantity, unitPrice)));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
    }
}
