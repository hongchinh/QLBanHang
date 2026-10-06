using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.Posting;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventoryPostingServiceTests : InventoryEngineTestBase
{
    public InventoryPostingServiceTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Allow_policy_accepts_negative_stock()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("PS01");

        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 3));

        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(-3m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warn_policy_without_acknowledgement_throws_warning_with_details()
    {
        await SetPolicyAsync(NegativeStockPolicy.Warn);
        var p = await CreateInventoryProductAsync("PS02");

        var post = () => PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 3));

        var ex = (await post.Should().ThrowAsync<NegativeStockException>()).Which;
        ex.Code.Should().Be("NEGATIVE_STOCK_WARNING");
        ex.IsWarning.Should().BeTrue();
        ex.Shortages.Should().ContainKey("PS02@KHO01")
            .WhoseValue.Should().Equal("Âm 3 Tấm tại 05/10/2026 08:00");
        (await LedgerAsync(p)).Should().BeEmpty();
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Warn_policy_with_acknowledgement_succeeds()
    {
        await SetPolicyAsync(NegativeStockPolicy.Warn);
        var p = await CreateInventoryProductAsync("PS03");

        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 3), acknowledge: true);

        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(-3m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Block_policy_rejects_even_when_acknowledged()
    {
        await SetPolicyAsync(NegativeStockPolicy.Block);
        var p = await CreateInventoryProductAsync("PS04", PricingMode.PerSquareMeter);

        var post = () => PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 1.5m), acknowledge: true);

        var ex = (await post.Should().ThrowAsync<NegativeStockException>()).Which;
        ex.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        ex.IsWarning.Should().BeFalse();
        ex.Shortages["PS04@KHO01"].Should().Equal("Âm 1.5 m² tại 05/10/2026 08:00");
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Posting_sets_outbound_cost()
    {
        var p = await CreateInventoryProductAsync("PS05");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 10, 1_000_000));
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 4));

        var outRow = (await LedgerAsync(p)).Single(r => r.QtyOut > 0);
        outRow.UnitCost.Should().Be(100_000m);
        outRow.CostAmount.Should().Be(400_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Rejected_posting_leaves_no_trace_after_rollback()
    {
        await SetPolicyAsync(NegativeStockPolicy.Block);
        var p = await CreateInventoryProductAsync("PS06");
        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-01 08:00", 2, 200_000));
        var before = await CountsAsync();

        var post = () => PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 5));
        await post.Should().ThrowAsync<NegativeStockException>();

        (await CountsAsync()).Should().Be(before);
        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(2m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Reducing_an_existing_deficit_passes_under_block()
    {
        await SetPolicyAsync(NegativeStockPolicy.Allow);
        var p = await CreateInventoryProductAsync("PS07");
        await PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-05 08:00", 10));
        await SetPolicyAsync(NegativeStockPolicy.Block);

        await PostAsync(InDraft(p, DefaultWarehouseId, "2026-10-03 08:00", 4, 400_000));
        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(-6m);

        var deeper = () => PostAsync(OutDraft(p, DefaultWarehouseId, "2026-10-04 08:00", 1));
        (await deeper.Should().ThrowAsync<NegativeStockException>()).Which.Code.Should().Be("NEGATIVE_STOCK_BLOCKED");
        (await BalanceAsync(p, DefaultWarehouseId)).Should().Be(-6m);
        await AssertInvariantsAsync();
    }

    private Task SetPolicyAsync(NegativeStockPolicy policy) =>
        UpdateInventorySettingsAsync(s => s.NegativeStockPolicy = policy);

    private Task<LedgerChangeResult> PostAsync(LedgerEntryDraft draft, bool acknowledge = false) =>
        InTransactionAsync(async (sp, ct) =>
        {
            var posting = sp.GetRequiredService<IInventoryPostingService>();
            await posting.AcquireLocksAsync(draft.BranchId, new[] { draft.ProductId }, ct);
            return await posting.PostAsync(draft.SourceType, draft.SourceId, new[] { draft }, acknowledge, ct);
        });

    private Task<(int Ledger, int Balances, int CostPeriods)> CountsAsync() =>
        InDbAsync(async db => (
            await db.InventoryLedger.CountAsync(),
            await db.StockBalances.CountAsync(),
            await db.InventoryCostPeriods.CountAsync()));
}
