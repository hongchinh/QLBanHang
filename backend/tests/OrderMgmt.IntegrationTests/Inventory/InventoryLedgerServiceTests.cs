using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventoryLedgerServiceTests : InventoryEngineTestBase
{
    public InventoryLedgerServiceTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Running_quantity_follows_posting_time_not_insert_order()
    {
        var p = await CreateInventoryProductAsync("LG01");
        await ReplaceAsync(InDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 10, 1_000_000));
        await ReplaceAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 3));
        await ReplaceAsync(InDraft(p, DefaultWarehouseId, "2026-10-03 08:00", 5, 500_000));

        var ledger = await LedgerAsync(p);
        ledger.Select(r => r.PostedAt).Should().Equal(Vn("2026-10-02 08:00"), Vn("2026-10-03 08:00"), Vn("2026-10-05 08:00"));
        ledger.Select(r => r.RunningQty).Should().Equal(10m, 15m, 12m);
        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(12m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Same_timestamp_orders_opening_then_in_then_out()
    {
        var p = await CreateInventoryProductAsync("LG02");
        const string at = "2026-10-01 00:00";
        await ReplaceAsync(OutDraft(p, DefaultWarehouseId, at, 6));
        await ReplaceAsync(InDraft(p, DefaultWarehouseId, at, 3, 300_000));
        await ReplaceAsync(LedgerSourceType.Opening, DefaultWarehouseId,
            new LedgerEntryDraft(p, DefaultWarehouseId, MainBranchId, Vn(at), LedgerSourceType.Opening,
                DefaultWarehouseId, Guid.NewGuid(), "TDK", 0, 5, 0, 500_000));

        var ledger = await LedgerAsync(p);
        ledger.Select(r => r.SourceType).Should().Equal(LedgerSourceType.Opening, LedgerSourceType.StockIn, LedgerSourceType.StockOut);
        ledger.Select(r => r.RunningQty).Should().Equal(5m, 8m, 2m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Moving_a_source_later_recomputes_from_the_earliest_time()
    {
        var p = await CreateInventoryProductAsync("LG03");
        var receipt = InDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 10, 1_000_000);
        await ReplaceAsync(receipt);
        await ReplaceAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 3));

        var result = await ReplaceAsync(receipt with { PostedAt = Vn("2026-10-06 08:00") });

        var ledger = await LedgerAsync(p);
        ledger.Select(r => r.QtyOut).Should().Equal(3m, 0m);
        ledger.Select(r => r.RunningQty).Should().Equal(-3m, 7m);

        var pair = result.Pairs.Single();
        pair.From.Should().Be(Vn("2026-10-02 08:00"));
        pair.MinRunningQty.Should().Be(-3m);
        pair.FirstNegativeAt.Should().Be(Vn("2026-10-05 08:00"));
        pair.OldMinRunningQty.Should().Be(7m);
        pair.OldFirstNegativeAt.Should().BeNull();
        pair.Balance.Should().Be(7m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Drafts_with_vn_offset_are_stored_as_utc()
    {
        var p = await CreateInventoryProductAsync("LG04");
        var local = new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.FromHours(7));

        await ReplaceAsync(InDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 1, 100_000) with { PostedAt = local });

        var stored = (await LedgerAsync(p)).Single().PostedAt;
        stored.Should().Be(local);
        stored.Offset.Should().Be(TimeSpan.Zero);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Replacing_with_empty_list_removes_rows_and_balance()
    {
        var p = await CreateInventoryProductAsync("LG05");
        var receipt = InDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 4, 400_000);
        await ReplaceAsync(receipt);

        var result = await ReplaceAsync(LedgerSourceType.StockIn, receipt.SourceId);

        (await LedgerAsync(p)).Should().BeEmpty();
        (await BalanceAsync(p, DefaultWarehouseId)).Should().BeNull();
        result.Pairs.Single().Should().Match<PairChange>(c => c.Balance == 0m && c.MinRunningQty == null && c.OldMinRunningQty == 4m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Moving_a_line_between_warehouses_reports_both_pairs()
    {
        var p = await CreateInventoryProductAsync("LG06");
        var otherWarehouseId = await CreateWarehouseAsync(MainBranchId, "KHO02");
        var receipt = InDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 10, 1_000_000);
        await ReplaceAsync(receipt);

        var result = await ReplaceAsync(receipt with { WarehouseId = otherWarehouseId });

        result.Pairs.Select(c => (c.WarehouseId, c.Balance))
            .Should().BeEquivalentTo(new[] { (DefaultWarehouseId, 0m), (otherWarehouseId, 10m) });
        (await BalanceAsync(p, DefaultWarehouseId)).Should().BeNull();
        (await BalanceAsync(p, otherWarehouseId)).Should().Be(10m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Balance_equals_sum_of_ledger_after_mixed_changes()
    {
        var p = await CreateInventoryProductAsync("LG07");
        var otherWarehouseId = await CreateWarehouseAsync(MainBranchId, "KHO02");
        var voucherId = Guid.NewGuid();

        await ReplaceAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 20, 2_000_000));
        await ReplaceAsync(LedgerSourceType.StockOut, voucherId,
            OutDraft(p, DefaultWarehouseId, "2026-10-04 08:00", 5, voucherId),
            OutDraft(p, otherWarehouseId, "2026-10-04 08:00", 2, voucherId) with { LineSortOrder = 1 });
        await ReplaceAsync(InDraft(p, otherWarehouseId, "2026-10-03 08:00", 6, 600_000));
        await ReplaceAsync(LedgerSourceType.StockOut, voucherId,
            OutDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 8, voucherId));

        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(12m);
        (await BalanceAsync(p, otherWarehouseId)).Should().Be(6m);
        (await InDbAsync(db => db.InventoryLedger.CountAsync(r => r.ProductId == p))).Should().Be(3);
        await AssertInvariantsAsync();
    }
}
