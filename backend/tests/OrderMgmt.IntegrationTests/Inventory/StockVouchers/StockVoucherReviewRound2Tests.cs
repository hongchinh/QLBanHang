using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

/// Regression tests for the second code review of the stock voucher API.
[Collection(nameof(PostgresCollection))]
public class StockVoucherReviewRound2Tests : InventoryTestBase
{
    public StockVoucherReviewRound2Tests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Editing_an_old_voucher_keeps_the_line_snapshot_after_the_product_changed()
    {
        var p = await CreateInventoryProductAsync("R2S01", track: false);
        var voucher = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 5, 10_000));
        await InDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync(x => x.Id == p);
            product.TrackInventory = true;
            product.PricingMode = PricingMode.PerSquareMeter;
            await db.SaveChangesAsync();
        });

        var request = UpdateRequestFrom(voucher);
        request.Note = "Chỉ sửa ghi chú";
        var (status, updated, error) = await PutVoucherAsync(_client, voucher.Id, request);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        updated!.Lines.Single().Should().BeEquivalentTo(new
        {
            TrackInventory = false, PricingMode = PricingMode.PerUnit, Quantity = 5m, Amount = 50_000m,
        });
        (await LedgerOfAsync(voucher.Id)).Should().BeEmpty();

        // A new line of the same product takes the current product (tracked, m²).
        var withNewLine = UpdateRequestFrom(updated);
        withNewLine.Lines.Add(new() { ProductId = p, SortOrder = 1, SheetCount = 2, Length = 1000, Width = 500, UnitPrice = 1_000 });
        var (newStatus, withNew, newError) = await PutVoucherAsync(_client, voucher.Id, withNewLine);
        newStatus.Should().Be(HttpStatusCode.OK, newError?.Message);
        withNew!.Lines.Select(l => (l.TrackInventory, l.PricingMode))
            .Should().Equal((false, PricingMode.PerUnit), (true, PricingMode.PerSquareMeter));
        (await LedgerOfAsync(voucher.Id)).Should().ContainSingle().Which.QtyIn.Should().Be(1m);
        await AssertInvariantsAsync();
    }
}
