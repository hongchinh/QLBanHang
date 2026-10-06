using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Costing;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventoryCostingServiceTests : InventoryEngineTestBase
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1), Oct1 = new(2026, 10, 1), Nov1 = new(2026, 11, 1);

    public InventoryCostingServiceTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Branch_scope_month_matches_hand_calculation()
    {
        var p = await CreateInventoryProductAsync("CS01");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 100, 5_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 30));
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-03 08:00", 50, 3_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-04 08:00", 40));

        (await OutCostsAsync(p)).Should().Equal(1_600_000m, 2_133_333m);
        var october = await PeriodAsync(p, MainBranchId, Oct1);
        october.Should().BeEquivalentTo(new
        {
            OpeningQty = 0m, OpeningValue = 0m, InQty = 150m, InValue = 8_000_000m, OutQty = 70m, OutValue = 3_733_333m,
            AvgCost = 53_333.3333m, ClosingQty = 80m, ClosingValue = 4_266_667m, PeriodEnd = new DateOnly(2026, 10, 31),
        });
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warehouse_scope_costs_each_warehouse_separately()
    {
        var p = await CreateInventoryProductAsync("CS02");
        var w2 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 10, 1_000_000));
        await PostAsync(InDraft(p, w2, "2026-10-01 09:00", 10, 2_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-02 08:00", 5));
        await PostAsync(OutDraft(p, w2, "2026-10-02 09:00", 5));

        (await OutCostsAsync(p)).Should().Equal(750_000m, 750_000m);

        await UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse);
        await RecalculateAsync(
            new CostScopeChange(p, MainBranchId, DefaultWarehouseId, Oct1),
            new CostScopeChange(p, MainBranchId, w2, Oct1));

        (await OutCostsAsync(p, DefaultWarehouseId)).Should().Equal(500_000m);
        (await OutCostsAsync(p, w2)).Should().Equal(1_000_000m);
        (await PeriodAsync(p, w2, Oct1)).AvgCost.Should().Be(200_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Back_dated_inbound_reprices_later_periods()
    {
        var p = await CreateInventoryProductAsync("CS03");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 5));
        (await OutCostsAsync(p)).Should().Equal(500_000m);

        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-20 08:00", 10, 3_000_000));

        var september = await PeriodAsync(p, MainBranchId, Sep1);
        september.AvgCost.Should().Be(200_000m);
        (await OutCostsAsync(p)).Should().Equal(1_000_000m);
        var october = await PeriodAsync(p, MainBranchId, Oct1);
        october.OpeningQty.Should().Be(20m).And.Be(september.ClosingQty);
        october.OpeningValue.Should().Be(4_000_000m).And.Be(september.ClosingValue);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Fully_locked_period_is_never_rewritten()
    {
        var p = await CreateInventoryProductAsync("CS04");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 5));
        var september = await PeriodAsync(p, MainBranchId, Sep1);
        var october = await PeriodAsync(p, MainBranchId, Oct1);

        await LockBranchAsync(new DateOnly(2026, 9, 30));
        await RecalculateAsync(new CostScopeChange(p, MainBranchId, MainBranchId, Sep1));

        (await PeriodAsync(p, MainBranchId, Sep1)).Should().BeEquivalentTo(september);
        var rewritten = await PeriodAsync(p, MainBranchId, Oct1);
        rewritten.Id.Should().NotBe(october.Id);
        rewritten.Should().BeEquivalentTo(october, o => o.Excluding(x => x.Id));
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Partially_locked_period_is_recomputed()
    {
        var p = await CreateInventoryProductAsync("CS05");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 5));
        (await OutCostsAsync(p)).Should().Equal(500_000m);

        await LockBranchAsync(new DateOnly(2026, 10, 10));
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-15 08:00", 10, 3_000_000));

        (await OutCostsAsync(p)).Should().Equal(1_000_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Fallback_uses_product_cost_price_when_no_history()
    {
        var p = await CreateInventoryProductAsync("CS06", costPrice: 60_000);
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 2));

        (await OutCostsAsync(p)).Should().Equal(120_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Empty_periods_between_movements_get_rows()
    {
        var p = await CreateInventoryProductAsync("CS07");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-11-05 08:00", 5));

        var october = await PeriodAsync(p, MainBranchId, Oct1);
        october.Should().BeEquivalentTo(new
        {
            OpeningQty = 10m, OpeningValue = 1_000_000m, InQty = 0m, OutQty = 0m,
            ClosingQty = 10m, ClosingValue = 1_000_000m,
        });
        (await PeriodAsync(p, MainBranchId, Nov1)).OutValue.Should().Be(500_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Year_period_averages_the_whole_year()
    {
        await UpdateInventorySettingsAsync(s => s.CostingPeriod = CostingPeriod.Year);
        var p = await CreateInventoryProductAsync("CS08");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-03-10 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-06-10 08:00", 5));
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-09-10 08:00", 10, 2_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-11-10 08:00", 5));

        var year = await PeriodAsync(p, MainBranchId, new DateOnly(2026, 1, 1));
        year.PeriodEnd.Should().Be(new DateOnly(2026, 12, 31));
        year.AvgCost.Should().Be(150_000m);
        (await OutCostsAsync(p)).Should().Equal(750_000m, 750_000m);
        (await InDbAsync(db => db.InventoryCostPeriods.CountAsync(c => c.ProductId == p && c.PeriodStart.Year == 2026)))
            .Should().Be(1);
        await AssertInvariantsAsync();
    }

    /// Ledger replacement followed by the cost recalculation of the affected scopes.
    private Task PostAsync(LedgerEntryDraft draft) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var change = await sp.GetRequiredService<IInventoryLedgerService>()
                .ReplaceSourceAsync(draft.SourceType, draft.SourceId, new[] { draft }, ct);
            var costing = sp.GetRequiredService<IInventoryCostingService>();
            await costing.RecalculateAsync(await costing.ScopesForAsync(change.Pairs, ct), ct);
            return change;
        });

    private Task RecalculateAsync(params CostScopeChange[] changes) =>
        InTransactionAsync(async (sp, ct) =>
        {
            await sp.GetRequiredService<IInventoryCostingService>().RecalculateAsync(changes, ct);
            return true;
        });

    private Task LockBranchAsync(DateOnly lockedUntil) =>
        InDbAsync(async db =>
        {
            var branch = await db.Branches.SingleAsync(b => b.Id == MainBranchId);
            branch.LockedUntil = lockedUntil;
            await db.SaveChangesAsync();
        });

    private Task<InventoryCostPeriod> PeriodAsync(Guid productId, Guid scopeKey, DateOnly periodStart) =>
        InDbAsync(db => db.InventoryCostPeriods.AsNoTracking()
            .SingleAsync(c => c.ProductId == productId && c.ScopeKey == scopeKey && c.PeriodStart == periodStart));

    /// CostAmount of the outbound rows, in posting order.
    private async Task<List<decimal?>> OutCostsAsync(Guid productId, Guid? warehouseId = null) =>
        (await LedgerAsync(productId, warehouseId)).Where(r => r.QtyOut > 0).Select(r => r.CostAmount).ToList();
}
