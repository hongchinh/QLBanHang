using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// Back-dated edits, deletes, cancels and restores must leave the same derived data
/// (ledger, cost periods, balances) as entering the surviving vouchers from scratch.
[Collection(nameof(PostgresCollection))]
public class InventoryInvariantTests : InventoryTestBase
{
    public InventoryInvariantTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public Task Back_dated_edits_deletes_and_cancels_match_re_entry_from_scratch() =>
        RunScriptAsync(CostingScope.Branch);

    [Fact]
    public Task Invariants_hold_under_warehouse_scope() =>
        RunScriptAsync(CostingScope.Warehouse);

    private async Task RunScriptAsync(CostingScope scope)
    {
        // No ledger data yet: a direct settings update needs no recalculation.
        await UpdateInventorySettingsAsync(s =>
        {
            s.NegativeStockPolicy = NegativeStockPolicy.Allow;
            s.CostingScope = scope;
        });
        var a = await CreateInventoryProductAsync("IV01");
        var b = await CreateInventoryProductAsync("IV02");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        void OnSecond(UpsertStockVoucherRequest r) => r.WarehouseId = second;

        // 1. About 8 vouchers, each with a distinct VoucherAt.
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-02 08:00",
            LineRequest(a, 10, 100_000), LineRequest(b, 20, 50_000),
            new UpsertStockVoucherLineRequest { ProductId = a, WarehouseId = second, Quantity = 2, UnitPrice = 105_000 });
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-10 08:00", OnSecond,
            LineRequest(a, 5, 120_000), LineRequest(b, 10, 55_000));
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-09-15 08:00", LineRequest(a, 4, 0), LineRequest(b, 5, 0));
        var movedIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-10 08:00", LineRequest(a, 10, 130_000));
        var deletedOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-09-20 08:00", OnSecond, LineRequest(a, 3, 0));
        var restoredIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 08:00", OnSecond,
            LineRequest(a, 8, 140_000), LineRequest(b, 6, 60_000));
        var cancelledOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-12 08:00", LineRequest(b, 10, 0));
        var changedOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-20 08:00",
            LineRequest(a, 6, 0), LineRequest(b, 4, 0));
        // Takes KHO02 negative for product A (policy Allow).
        await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-25 08:00", OnSecond, LineRequest(a, 18, 0));

        // 2. Back-dated changes.
        var move = UpdateRequestFrom(movedIn);
        move.VoucherAt = Vn("2026-09-05 08:00");
        move.Lines[0].UnitPrice = 110_000;
        var (moveStatus, _, moveError) = await PutVoucherAsync(_client, movedIn.Id, move);
        moveStatus.Should().Be(HttpStatusCode.OK, moveError?.Message);

        (await _client.DeleteAsync($"/api/stock-vouchers/{deletedOut.Id}?version={deletedOut.Version}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await ActionAsync(restoredIn.Id, "cancel", restoredIn.Version);
        await ActionAsync(restoredIn.Id, "restore", cancelled.Version);
        await ActionAsync(cancelledOut.Id, "cancel", cancelledOut.Version);

        var change = UpdateRequestFrom(changedOut);
        change.Lines.Single(l => l.ProductId == a).Quantity = 9;
        var (changeStatus, _, changeError) = await PutVoucherAsync(_client, changedOut.Id, change);
        changeStatus.Should().Be(HttpStatusCode.OK, changeError?.Message);

        // 3. Snapshot A.
        var before = await SnapshotAsync();
        before.Ledger.Should().NotBeEmpty();
        before.Periods.Should().NotBeEmpty();
        await AssertInvariantsAsync();

        // 4. Read the surviving vouchers, then wipe every voucher and all derived data.
        var survivors = new List<StockVoucherDto>();
        foreach (var type in new[] { StockDirection.In, StockDirection.Out })
        {
            var list = await ReadDataAsync<StockVoucherListResult>(await _client.GetAsync(
                $"/api/stock-vouchers?type={type}&status={StockVoucherStatus.Active}&pageSize=200"));
            foreach (var item in list.Items)
                survivors.Add(await GetVoucherAsync(_client, item.Id));
        }
        survivors.Should().HaveCount(7);

        await InDbAsync(async db =>
        {
            await db.StockVoucherActivities.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.StockVoucherLines.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.StockVouchers.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.InventoryLedger.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.InventoryCostPeriods.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.StockBalances.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.DocumentCounters.IgnoreQueryFilters().ExecuteDeleteAsync();
        });

        // 5. Re-enter them in VoucherAt order and compare.
        foreach (var voucher in survivors.OrderBy(v => v.VoucherAt))
        {
            var request = UpdateRequestFrom(voucher);
            request.Version = null;
            request.Lines.ForEach(l => l.Id = null);            var (status, _, error) = await PostVoucherAsync(_client, request);
            status.Should().Be(HttpStatusCode.OK, error?.Message);
        }

        var after = await SnapshotAsync();
        after.Ledger.Should().Equal(before.Ledger);
        after.Periods.Should().Equal(before.Periods);
        after.Balances.Should().Equal(before.Balances);
        await AssertInvariantsAsync();
    }

    private async Task<StockVoucherDto> ActionAsync(Guid id, string action, uint version)
    {
        var (status, voucher, error) = await ReadVoucherResponseAsync(await _client.PostAsJsonAsync(
            $"/api/stock-vouchers/{id}/{action}", new StockVoucherActionRequest { Version = version }));
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private record LedgerRow(Guid ProductId, Guid WarehouseId, DateTimeOffset PostedAt, decimal QtyIn, decimal QtyOut,
        decimal InValue, decimal RunningQty, decimal? CostAmount);

    private record PeriodRow(Guid ProductId, Guid BranchId, Guid ScopeKey, DateOnly PeriodStart, DateOnly PeriodEnd,
        decimal OpeningQty, decimal OpeningValue, decimal InQty, decimal InValue, decimal OutQty, decimal OutValue,
        decimal AvgCost, decimal ClosingQty, decimal ClosingValue);

    private record BalanceRow(Guid ProductId, Guid WarehouseId, Guid BranchId, decimal Quantity);

    private record Snapshot(List<LedgerRow> Ledger, List<PeriodRow> Periods, List<BalanceRow> Balances);

    /// Derived data without ids or source codes (re-entered vouchers get new ones), in a deterministic order.
    private Task<Snapshot> SnapshotAsync() =>
        InDbAsync(async db =>
        {
            var ledger = (await db.InventoryLedger.AsNoTracking().ToListAsync())
                .Select(r => new LedgerRow(r.ProductId, r.WarehouseId, r.PostedAt.ToUniversalTime(), r.QtyIn, r.QtyOut,
                    r.InValue, r.RunningQty, r.CostAmount))
                .OrderBy(r => r.ProductId).ThenBy(r => r.WarehouseId).ThenBy(r => r.PostedAt)
                .ThenBy(r => r.QtyIn).ThenBy(r => r.QtyOut)
                .ToList();
            var periods = (await db.InventoryCostPeriods.AsNoTracking().ToListAsync())
                .Select(p => new PeriodRow(p.ProductId, p.BranchId, p.ScopeKey, p.PeriodStart, p.PeriodEnd,
                    p.OpeningQty, p.OpeningValue, p.InQty, p.InValue, p.OutQty, p.OutValue, p.AvgCost,
                    p.ClosingQty, p.ClosingValue))
                .OrderBy(p => p.ProductId).ThenBy(p => p.ScopeKey).ThenBy(p => p.PeriodStart)
                .ToList();
            var balances = (await db.StockBalances.AsNoTracking().ToListAsync())
                .Select(x => new BalanceRow(x.ProductId, x.WarehouseId, x.BranchId, x.Quantity))
                .OrderBy(x => x.ProductId).ThenBy(x => x.WarehouseId)
                .ToList();
            return new Snapshot(ledger, periods, balances);
        });
}
