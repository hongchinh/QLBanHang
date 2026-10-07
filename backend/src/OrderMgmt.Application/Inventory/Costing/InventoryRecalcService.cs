using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Costing;

public class InventoryRecalcService : IInventoryRecalcService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;
    private readonly IInventoryLock _lock;
    private readonly IInventoryCostingService _costing;

    public InventoryRecalcService(IAppDbContext db, ICurrentBranch currentBranch, ITransactionRunner transaction,
        IInventoryLock inventoryLock, IInventoryCostingService costing)
    {
        _db = db;
        _currentBranch = currentBranch;
        _transaction = transaction;
        _lock = inventoryLock;
        _costing = costing;
    }

    public async Task<RecalculateCostResult> RecalculateAsync(RecalculateCostRequest request, CancellationToken ct = default)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);

        return await _transaction.RunAsync(async c =>
        {
            // Exclusive gate only, no product keys (D30); settings are read after it.
            await _lock.AcquireBranchGateAsync(new[] { branchId }, exclusive: true, c);
            var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, c);

            var period = CostingPeriodCalendar.PeriodOf(request.FromPeriodStart, settings.CostingPeriod);
            if (period.Start != request.FromPeriodStart)
                throw new ValidationDomainException(new Dictionary<string, string[]>
                {
                    ["fromPeriodStart"] = new[] { "Phải là ngày đầu kỳ tính giá" },
                }, null);
            var lockedUntil = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.LockedUntil).SingleAsync(c);
            if (lockedUntil is { } locked && period.End <= locked)
                throw new DomainException("PERIOD_LOCKED",
                    string.Create(CultureInfo.InvariantCulture, $"Kỳ tính giá đã khóa sổ (đến {locked:dd/MM/yyyy})."));

            var changes = await ChangesAsync(branchId, request, settings.CostingScope, c);
            await _costing.RecalculateAsync(changes, c);
            return new RecalculateCostResult { FromPeriodStart = request.FromPeriodStart, ScopeCount = changes.Count };
        }, ct);
    }

    /// Every (product, scope key) with ledger rows in the branch, after the request filters. A scope starts at
    /// FromPeriodStart, or at its first row when that is later (no empty periods before the first movement).
    private async Task<List<CostScopeChange>> ChangesAsync(Guid branchId, RecalculateCostRequest request,
        CostingScope scope, CancellationToken ct)
    {
        var rows = _db.InventoryLedger.AsNoTracking().Where(e => e.BranchId == branchId);
        if (request.ProductId is { } productId)
            rows = rows.Where(e => e.ProductId == productId);
        if (request.WarehouseId is { } warehouseId)
        {
            if (scope == CostingScope.Warehouse)
            {
                rows = rows.Where(e => e.WarehouseId == warehouseId);
            }
            else
            {
                var productIds = await rows.Where(e => e.WarehouseId == warehouseId)
                    .Select(e => e.ProductId).Distinct().ToListAsync(ct);
                rows = rows.Where(e => productIds.Contains(e.ProductId));
            }
        }

        var scopes = scope == CostingScope.Branch
            ? rows.GroupBy(e => new { e.ProductId, ScopeKey = e.BranchId })
                .Select(g => new { g.Key.ProductId, g.Key.ScopeKey, First = g.Min(e => e.PostedAt) })
            : rows.GroupBy(e => new { e.ProductId, ScopeKey = e.WarehouseId })
                .Select(g => new { g.Key.ProductId, g.Key.ScopeKey, First = g.Min(e => e.PostedAt) });

        return (await scopes.ToListAsync(ct))
            .Select(s =>
            {
                var firstDate = VnTime.ToVnDate(s.First);
                return new CostScopeChange(s.ProductId, branchId, s.ScopeKey,
                    firstDate > request.FromPeriodStart ? firstDate : request.FromPeriodStart);
            })
            .ToList();
    }
}
