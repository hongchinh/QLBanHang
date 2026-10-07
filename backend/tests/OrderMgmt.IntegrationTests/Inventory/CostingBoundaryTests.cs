using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// Costing period boundaries in VN time (D14, D27) and the average-cost fallback (D7, D37), through
/// IInventoryPostingService with the default Month / Branch settings.
[Collection(nameof(PostgresCollection))]
public class CostingBoundaryTests : InventoryEngineTestBase
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Oct1 = new(2026, 10, 1), Nov1 = new(2026, 11, 1);

    public CostingBoundaryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Outbound_after_vn_midnight_lands_in_the_next_month()
    {
        var p = await CreateInventoryProductAsync("CB01");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 10, 1_000_000));
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-11-02 08:00", 10, 3_000_000));
        // 2026-10-31 23:30 VN = 16:30 UTC: October, average 100,000.
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-31 23:30", 1));
        // 2026-11-01 00:30 VN = 2026-10-31 17:30 UTC: November, average (900,000 + 3,000,000) / 19.
        var lateOut = OutDraft(p, DefaultWarehouseId, "2026-11-01 00:30", 1);
        lateOut.PostedAt.Should().Be(new DateTimeOffset(2026, 10, 31, 17, 30, 0, TimeSpan.Zero));
        await PostAsync(lateOut);

        (await OutCostsAsync(p)).Should().Equal(100_000m, 205_263m);
        (await PeriodAsync(p, Oct1)).Should().BeEquivalentTo(new { OutQty = 1m, OutValue = 100_000m, ClosingQty = 9m });
        (await PeriodAsync(p, Nov1)).Should().BeEquivalentTo(new
        {
            OpeningQty = 9m, OpeningValue = 900_000m, InQty = 10m, OutQty = 1m, AvgCost = 205_263.1579m, OutValue = 205_263m,
        });
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Run_from_a_later_period_falls_back_to_the_previous_period_avg_cost()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("CB02", costPrice: 60_000);
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-09-20 08:00", 10));

        // October starts empty: the run starts in October, so the average comes from the stored September row
        // (100,000), not from Product.CostPrice (60,000).
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 2));

        (await PeriodAsync(p, Sep1)).AvgCost.Should().Be(100_000m);
        (await OutCostsAsync(p)).Should().Equal(1_000_000m, 200_000m);
        (await PeriodAsync(p, Oct1)).Should().BeEquivalentTo(new { AvgCost = 100_000m, ClosingQty = -2m, ClosingValue = -200_000m });
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Negative_average_falls_back_to_the_previous_period_avg_cost()
    {
        await UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("CB03", costPrice: 60_000);
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-09-20 08:00", 10));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 5));

        // November: quantity −5 + 10 > 0, value −500,000 + 200,000 < 0 → October's 100,000, never a negative average (D37).
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-11-02 08:00", 10, 200_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-11-10 08:00", 1));

        (await PeriodAsync(p, Nov1)).Should().BeEquivalentTo(new
        {
            OpeningQty = -5m, OpeningValue = -500_000m, InQty = 10m, InValue = 200_000m, AvgCost = 100_000m, OutValue = 100_000m,
        });
        (await OutCostsAsync(p)).Should().Equal(1_000_000m, 500_000m, 100_000m);
        await AssertInvariantsAsync();
    }

    private Task<LedgerChangeResult> PostAsync(LedgerEntryDraft draft) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var posting = sp.GetRequiredService<IInventoryPostingService>();
            await posting.AcquireLocksAsync(draft.BranchId, new[] { draft.ProductId }, ct);
            return await posting.PostAsync(draft.SourceType, draft.SourceId, new[] { draft }, true, ct);
        });

    private Task<InventoryCostPeriod> PeriodAsync(Guid productId, DateOnly periodStart) =>
        InDbAsync(db => db.InventoryCostPeriods.AsNoTracking()
            .SingleAsync(c => c.ProductId == productId && c.ScopeKey == MainBranchId && c.PeriodStart == periodStart));

    /// CostAmount of the outbound rows, in posting order.
    private async Task<List<decimal?>> OutCostsAsync(Guid productId) =>
        (await LedgerAsync(productId)).Where(r => r.QtyOut > 0).Select(r => r.CostAmount).ToList();
}
