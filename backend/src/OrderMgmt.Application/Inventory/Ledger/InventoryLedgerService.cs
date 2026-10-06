using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Ledger;

public class InventoryLedgerService : IInventoryLedgerService
{
    private readonly IAppDbContext _db;

    public InventoryLedgerService(IAppDbContext db) => _db = db;

    public async Task<LedgerChangeResult> ReplaceSourceAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, CancellationToken ct = default)
    {
        var drafts = entries.Select(e => e with { PostedAt = VnTime.ToUtc(e.PostedAt) }).ToList();
        var sourceRows = _db.InventoryLedger.Where(e => e.SourceType == sourceType && e.SourceId == sourceId);

        // 1. Affected pairs and their windows, from the old rows and the new drafts.
        var oldRows = await sourceRows
            .Select(e => new { e.ProductId, e.WarehouseId, e.BranchId, e.PostedAt })
            .ToListAsync(ct);
        var pairs = oldRows.Select(r => (r.ProductId, r.WarehouseId, r.BranchId, r.PostedAt))
            .Concat(drafts.Select(d => (d.ProductId, d.WarehouseId, d.BranchId, d.PostedAt)))
            .GroupBy(r => (r.ProductId, r.WarehouseId))
            .Select(g => (g.Key.ProductId, g.Key.WarehouseId, g.Last().BranchId, From: g.Min(r => r.PostedAt)))
            .ToList();

        // The pre-change state of each window, read before the old rows go (D31).
        var before = new List<(decimal? Min, DateTimeOffset? FirstNegativeAt)>();
        foreach (var p in pairs)
        {
            var window = PairRows(p.ProductId, p.WarehouseId).Where(e => e.PostedAt >= p.From);
            before.Add((
                await window.MinAsync(e => (decimal?)e.RunningQty, ct),
                await window.Where(e => e.RunningQty < 0).InPostingOrder()
                    .Select(e => (DateTimeOffset?)e.PostedAt).FirstOrDefaultAsync(ct)));
        }

        await sourceRows.ExecuteDeleteAsync(ct);

        // 2. New rows; RunningQty is filled in below.
        _db.InventoryLedger.AddRange(drafts.Select(d => new InventoryLedgerEntry
        {
            PostedAt = d.PostedAt,
            SourceType = d.SourceType,
            SourceId = d.SourceId,
            SourceLineId = d.SourceLineId,
            SourceCode = d.SourceCode,
            LineSortOrder = d.LineSortOrder,
            BranchId = d.BranchId,
            WarehouseId = d.WarehouseId,
            ProductId = d.ProductId,
            QtyIn = d.QtyIn,
            QtyOut = d.QtyOut,
            InValue = d.InValue,
        }));
        await _db.SaveChangesAsync(ct);

        // 4. Running quantities from each window start, then the balance.
        var changes = new List<PairChange>(pairs.Count);
        for (var i = 0; i < pairs.Count; i++)
        {
            var p = pairs[i];
            var baseQty = await PairRows(p.ProductId, p.WarehouseId)
                .Where(e => e.PostedAt < p.From)
                .InReversePostingOrder()
                .Select(e => (decimal?)e.RunningQty)
                .FirstOrDefaultAsync(ct);
            var rows = await PairRows(p.ProductId, p.WarehouseId)
                .Where(e => e.PostedAt >= p.From)
                .InPostingOrder()
                .ToListAsync(ct);

            var running = baseQty ?? 0m;
            decimal? min = null;
            DateTimeOffset? firstNegativeAt = null;
            foreach (var row in rows)
            {
                running += row.QtyIn - row.QtyOut;
                row.RunningQty = running;
                if (min is null || running < min)
                    min = running;
                if (running < 0 && firstNegativeAt is null)
                    firstNegativeAt = row.PostedAt;
            }

            var balance = await _db.StockBalances.FindAsync(new object[] { p.WarehouseId, p.ProductId }, ct);
            if (baseQty is null && rows.Count == 0)
            {
                if (balance is not null)
                    _db.StockBalances.Remove(balance);
            }
            else if (balance is null)
            {
                _db.StockBalances.Add(new StockBalance
                {
                    WarehouseId = p.WarehouseId,
                    ProductId = p.ProductId,
                    BranchId = p.BranchId,
                    Quantity = running,
                });
            }
            else
            {
                balance.Quantity = running;
            }

            changes.Add(new PairChange(p.ProductId, p.WarehouseId, p.BranchId, p.From, min, firstNegativeAt,
                before[i].Min, before[i].FirstNegativeAt, running));
        }

        await _db.SaveChangesAsync(ct);
        return new LedgerChangeResult(changes);
    }

    private IQueryable<InventoryLedgerEntry> PairRows(Guid productId, Guid warehouseId) =>
        _db.InventoryLedger.Where(e => e.ProductId == productId && e.WarehouseId == warehouseId);
}
