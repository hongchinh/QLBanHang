using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// D31 before/after comparison and the D17 shortage message, through IInventoryPostingService.
[Collection(nameof(PostgresCollection))]
public class NegativeStockCheckTests : InventoryEngineTestBase
{
    public NegativeStockCheckTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Message_reports_the_deepest_point_with_its_own_time()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("NC01");
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-07 08:00", 9));
        await SetPolicyAsync(NegativeStockPolicy.Warn);

        // −1 at 10-03, then −10 at 10-07: the minimum is reported at 10-07, not at the first negative point.
        var post = () => PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-03 08:00", 1));

        var ex = (await post.Should().ThrowAsync<NegativeStockException>()).Which;
        ex.Shortages.Should().ContainKey("NC01@KHO01")
            .WhoseValue.Should().Equal("Âm 10 Tấm tại 07/10/2026 08:00");
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task First_negative_point_moved_to_an_earlier_date_is_flagged()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("NC02");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 5, 500_000));
        var outbound = OutDraft(p, DefaultWarehouseId, "2026-10-07 08:00", 6);
        await PostAsync(outbound);
        await SetPolicyAsync(NegativeStockPolicy.Block);

        // Same depth (−1), but from 10-03 instead of 10-07.
        var moved = () => PostAsync(outbound with { PostedAt = Vn("2026-10-03 08:00") });

        (await moved.Should().ThrowAsync<NegativeStockException>()).Which.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await LedgerAsync(p)).Single(r => r.QtyOut > 0).PostedAt.Should().Be(Vn("2026-10-07 08:00"));
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task First_negative_point_moved_earlier_at_the_same_instant_is_flagged()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("NC03");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 5, 500_000));
        var sourceId = Guid.NewGuid();
        // Two lines of one voucher: 5 → 3 → −1, the first negative point is line 1.
        var line0 = OutDraft(p, DefaultWarehouseId, "2026-10-07 08:00", 2, sourceId);
        var line1 = line0 with { LineSortOrder = 1, QtyOut = 4 };
        await PostAsync(line0, line1);
        await SetPolicyAsync(NegativeStockPolicy.Block);

        // 5 → −1 → −1: same depth and same PostedAt, but the first negative row is now line 0 (D34 position).
        var moved = () => PostAsync(line0 with { QtyOut = 6 }, line1 with { QtyOut = 0 });

        (await moved.Should().ThrowAsync<NegativeStockException>()).Which.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Same_deficit_at_the_same_point_is_not_flagged()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("NC04");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 5, 500_000));
        var sourceId = Guid.NewGuid();
        var line0 = OutDraft(p, DefaultWarehouseId, "2026-10-07 08:00", 2, sourceId);
        var line1 = line0 with { LineSortOrder = 1, QtyOut = 4 };
        await PostAsync(line0, line1);
        await SetPolicyAsync(NegativeStockPolicy.Block);

        // Re-posting rewrites the rows with new ids; the deficit keeps its depth and its position, so it passes.
        // Repeated, so a random new id that sorts before the old one would surface.
        for (var i = 0; i < 5; i++)
            await PostAsync(line0, line1);
        // A later receipt that only reduces the deficit passes too.
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-08 08:00", 1, 100_000));

        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(0m);
        await AssertInvariantsAsync();
    }

    private Task SetPolicyAsync(NegativeStockPolicy policy) =>
        UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = policy);

    /// Posts the drafts of one source (they share SourceType and SourceId), without acknowledgement.
    private Task<LedgerChangeResult> PostAsync(params LedgerEntryDraft[] drafts) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var posting = sp.GetRequiredService<IInventoryPostingService>();
            await posting.AcquireLocksAsync(MainBranchId, drafts.Select(d => d.ProductId), ct);
            return await posting.PostAsync(drafts[0].SourceType, drafts[0].SourceId, drafts, false, ct);
        });
}
