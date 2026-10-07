using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.Inventory;
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

    public async Task ApplySettingsChangeAsync(InventorySettings before, InventorySettings after, CancellationToken ct = default)
    {
        var periodChanged = before.CostingPeriod != after.CostingPeriod;
        var scopeChanged = before.CostingScope != after.CostingScope;
        var vatChanged = before.PurchaseCostIncludesVat != after.PurchaseCostIncludesVat;
        if (!periodChanged && !scopeChanged && !vatChanged)
            return;

        // Exclusive gate of every branch, not only those with rows yet, so an in-flight first posting that read the old
        // settings is waited for and then included (D30). No product keys.
        var allBranchIds = await _db.Branches.IgnoreQueryFilters().Select(b => b.Id).ToListAsync(ct);
        await _lock.AcquireBranchGateAsync(allBranchIds, exclusive: true, ct);

        var branches = await _db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => _db.InventoryLedger.Any(e => e.BranchId == b.Id))
            .OrderBy(b => b.Code)
            .Select(b => new { b.Id, b.Code, b.LockedUntil })
            .ToListAsync(ct);

        // Validate every branch before writing anything (D11, D33).
        var errors = new Dictionary<string, List<string>>();
        foreach (var branch in branches.Where(b => b.LockedUntil.HasValue))
        {
            var locked = branch.LockedUntil!.Value;
            if (periodChanged && !CostingPeriodCalendar.IsPeriodEnd(locked, after.CostingPeriod))
                AddError(errors, "costingPeriod", string.Create(CultureInfo.InvariantCulture,
                    $"Ngày khóa sổ của chi nhánh {branch.Code} ({locked:dd/MM/yyyy}) phải là ngày cuối kỳ khi đổi kỳ tính giá."));
            if (vatChanged && !CostingPeriodCalendar.IsPeriodEnd(locked, before.CostingPeriod))
                AddError(errors, "purchaseCostIncludesVat", string.Create(CultureInfo.InvariantCulture,
                    $"Ngày khóa sổ của chi nhánh {branch.Code} ({locked:dd/MM/yyyy}) phải là ngày cuối kỳ khi đổi cách tính VAT vào giá nhập."));
        }
        if (errors.Count > 0)
            throw new ValidationDomainException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()),
                errors.Values.First()[0]);

        var vatFactor = after.PurchaseCostIncludesVat ? 1m : 0m;
        var changes = new List<CostScopeChange>();
        foreach (var branch in branches)
        {
            var rows = _db.InventoryLedger.Where(e => e.BranchId == branch.Id);

            if (vatChanged)
            {
                var stockIn = rows.Where(e => e.SourceType == LedgerSourceType.StockIn);
                if (branch.LockedUntil is { } locked)
                {
                    var unlockedFrom = VnTime.StartOfNextDay(locked);
                    stockIn = stockIn.Where(e => e.PostedAt >= unlockedFrom);
                }
                await stockIn.ExecuteUpdateAsync(s => s.SetProperty(e => e.InValue, e => _db.StockVoucherLines
                    .IgnoreQueryFilters()
                    .Where(l => l.Id == e.SourceLineId)
                    .Select(l => l.InboundValue + l.VatAmount * vatFactor)
                    .First()), ct);
            }

            if (periodChanged || scopeChanged)
            {
                var keepUntil = branch.LockedUntil ?? DateOnly.MinValue;
                await _db.InventoryCostPeriods
                    .Where(c => c.BranchId == branch.Id && c.PeriodEnd > keepUntil)
                    .ExecuteDeleteAsync(ct);
            }

            var notBefore = branch.LockedUntil?.AddDays(1) ?? DateOnly.MinValue;
            changes.AddRange((await FirstDatesAsync(rows, after.CostingScope, ct))
                .Select(s => new CostScopeChange(s.ProductId, branch.Id, s.ScopeKey,
                    s.FirstDate > notBefore ? s.FirstDate : notBefore)));
        }

        await _costing.RecalculateAsync(changes, ct);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
            errors[key] = messages = new List<string>();
        messages.Add(message);
    }

    /// The first VN ledger date of every (product, scope key) of the rows.
    private static async Task<List<(Guid ProductId, Guid ScopeKey, DateOnly FirstDate)>> FirstDatesAsync(
        IQueryable<InventoryLedgerEntry> rows, CostingScope scope, CancellationToken ct)
    {
        var scopes = scope == CostingScope.Branch
            ? rows.GroupBy(e => new { e.ProductId, ScopeKey = e.BranchId })
                .Select(g => new { g.Key.ProductId, g.Key.ScopeKey, First = g.Min(e => e.PostedAt) })
            : rows.GroupBy(e => new { e.ProductId, ScopeKey = e.WarehouseId })
                .Select(g => new { g.Key.ProductId, g.Key.ScopeKey, First = g.Min(e => e.PostedAt) });

        return (await scopes.ToListAsync(ct))
            .Select(s => (s.ProductId, s.ScopeKey, VnTime.ToVnDate(s.First)))
            .ToList();
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

        return (await FirstDatesAsync(rows, scope, ct))
            .Select(s => new CostScopeChange(s.ProductId, branchId, s.ScopeKey,
                s.FirstDate > request.FromPeriodStart ? s.FirstDate : request.FromPeriodStart))
            .ToList();
    }
}
