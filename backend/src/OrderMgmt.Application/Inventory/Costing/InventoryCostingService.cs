using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Costing;

public class InventoryCostingService : IInventoryCostingService
{
    private readonly IAppDbContext _db;
    private readonly IDateTime _clock;

    public InventoryCostingService(IAppDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CostScopeChange>> ScopesForAsync(IReadOnlyList<PairChange> pairs, CancellationToken ct = default)
    {
        var scope = (await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct)).CostingScope;
        return pairs
            .GroupBy(p => (p.ProductId, p.BranchId, ScopeKey: scope == CostingScope.Branch ? p.BranchId : p.WarehouseId))
            .Select(g => new CostScopeChange(g.Key.ProductId, g.Key.BranchId, g.Key.ScopeKey,
                VnTime.ToVnDate(g.Min(p => p.From))))
            .ToList();
    }

    public async Task RecalculateAsync(IReadOnlyCollection<CostScopeChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0)
            return;

        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
        var today = VnTime.ToVnDate(_clock.UtcNow);

        foreach (var change in changes)
        {
            var lockedUntil = await _db.Branches
                .Where(b => b.Id == change.BranchId)
                .Select(b => b.LockedUntil)
                .SingleAsync(ct);
            var start = CostingPeriodCalendar.FirstUnlockedFrom(change.FromDate, lockedUntil, settings.CostingPeriod);

            var scopeRows = settings.CostingScope == CostingScope.Branch
                ? _db.InventoryLedger.Where(e => e.ProductId == change.ProductId && e.BranchId == change.ScopeKey)
                : _db.InventoryLedger.Where(e => e.ProductId == change.ProductId && e.WarehouseId == change.ScopeKey);

            var lastPostedAt = await scopeRows.MaxAsync(e => (DateTimeOffset?)e.PostedAt, ct);
            var lastDate = lastPostedAt.HasValue && VnTime.ToVnDate(lastPostedAt.Value) > today
                ? VnTime.ToVnDate(lastPostedAt.Value)
                : today;
            var end = CostingPeriodCalendar.PeriodOf(lastDate, settings.CostingPeriod);

            // Opening from the ledger itself (D7).
            var startAt = VnTime.StartOfDay(start.Start);
            var beforeStart = scopeRows.Where(e => e.PostedAt < startAt);
            var openingQty = await beforeStart.SumAsync(e => e.QtyIn - e.QtyOut, ct);
            var openingValue = await beforeStart.SumAsync(e => e.InValue - (e.CostAmount ?? 0m), ct);

            var fallback = await _db.InventoryCostPeriods
                    .Where(c => c.ProductId == change.ProductId && c.ScopeKey == change.ScopeKey && c.PeriodStart < start.Start)
                    .OrderByDescending(c => c.PeriodStart)
                    .Select(c => (decimal?)c.AvgCost)
                    .FirstOrDefaultAsync(ct)
                ?? await _db.Products.Where(p => p.Id == change.ProductId).Select(p => p.CostPrice).FirstOrDefaultAsync(ct)
                ?? 0m;

            var endAt = VnTime.StartOfNextDay(end.End);
            var rows = await scopeRows
                .Where(e => e.PostedAt >= startAt && e.PostedAt < endAt)
                .InPostingOrder()
                .ToListAsync(ct);

            var periods = CostingPeriodCalendar.Range(start.Start, end.End, settings.CostingPeriod);
            var result = PeriodicAverageCalculator.Run(openingQty, openingValue, fallback, Bucket(rows, periods));

            await _db.InventoryCostPeriods
                .Where(c => c.ProductId == change.ProductId && c.ScopeKey == change.ScopeKey && c.PeriodStart >= start.Start)
                .ExecuteDeleteAsync(ct);
            _db.InventoryCostPeriods.AddRange(result.Periods.Select(p => new InventoryCostPeriod
            {
                ProductId = change.ProductId,
                BranchId = change.BranchId,
                ScopeKey = change.ScopeKey,
                PeriodStart = p.Start,
                PeriodEnd = p.End,
                OpeningQty = p.OpeningQty,
                OpeningValue = p.OpeningValue,
                InQty = p.InQty,
                InValue = p.InValue,
                OutQty = p.OutQty,
                OutValue = p.OutValue,
                AvgCost = p.AvgCost,
                ClosingQty = p.ClosingQty,
                ClosingValue = p.ClosingValue,
            }));

            var rowsById = rows.ToDictionary(r => r.Id);
            foreach (var cost in result.OutCosts)
            {
                var row = rowsById[cost.EntryId];
                row.UnitCost = cost.UnitCost;
                row.CostAmount = cost.CostAmount;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// Rows are in posting order and lie inside the contiguous periods, so one pass assigns each row by its VN date.
    private static List<CostPeriodInput> Bucket(IReadOnlyList<InventoryLedgerEntry> rows, IReadOnlyList<CostingPeriodRange> periods)
    {
        var inputs = new List<CostPeriodInput>(periods.Count);
        var next = 0;
        foreach (var period in periods)
        {
            var movements = new List<CostMovement>();
            while (next < rows.Count && VnTime.ToVnDate(rows[next].PostedAt) <= period.End)
            {
                var row = rows[next++];
                movements.Add(new CostMovement(row.Id, row.QtyIn, row.QtyOut, row.InValue));
            }
            inputs.Add(new CostPeriodInput(period.Start, period.End, movements));
        }
        return inputs;
    }
}
