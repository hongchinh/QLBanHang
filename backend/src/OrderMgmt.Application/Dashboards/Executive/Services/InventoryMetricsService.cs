using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Inventory of the executive dashboard, read from the stock ledger and the current quantities the stock-on-hand report
/// reads. The stock is valued now: every ledger row counts, whatever the period, and the value of a product in a
/// warehouse is what came in less what went out at cost. Values need inventory.view_cost; counts are always shown.
public class InventoryMetricsService : IInventoryMetricsService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public InventoryMetricsService(IAppDbContext db, ICurrentUser currentUser, IExecutiveScopeResolver scopes,
        ExecutiveLookups lookups, IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _currentUser = currentUser;
        _scopes = scopes;
        _lookups = lookups;
        _clock = clock;
        _transaction = transaction;
    }

    public Task<InventorySummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSummaryAsync(query, c), ct);

    private async Task<InventorySummaryDto> BuildSummaryAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);

        var balances = await LoadBalancesAsync(scope, ct);
        var inStock = balances.Where(b => b.Quantity != 0m).ToList();
        var summary = new InventorySummaryDto
        {
            From = period.From,
            To = period.To,
            CanViewCost = canViewCost,
            ProductCount = inStock.Select(b => b.ProductId).Distinct().Count(),
            WarehouseCount = inStock.Select(b => b.WarehouseId).Distinct().Count(),
            NegativeCount = BelowZero(balances).Count(),
        };
        if (!canViewCost)
            return summary;

        // Values are read only when the user may see them.
        var pairs = await LoadPairsAsync(scope, ct);
        var (inValue, outCost) = await LoadPeriodMovementAsync(scope, period, ct);
        summary.StockValue = pairs.Sum(p => p.Value);
        summary.InValue = inValue;
        summary.OutCost = outCost;
        return summary;
    }

    /// The quantities now (kept up to date on every ledger change) of every product and warehouse of the scope.
    private async Task<List<BalanceRow>> LoadBalancesAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.StockBalances.AsNoTracking()
            .Where(b => branchIds.Contains(b.BranchId))
            .Select(b => new BalanceRow(b.BranchId, b.ProductId, b.WarehouseId, b.Quantity))
            .ToListAsync(ct);
    }

    /// The ledger of the scope summed by branch, product and warehouse: the quantity and the value. No date filter: the
    /// stock is valued now.
    private async Task<List<Pair>> LoadPairsAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.InventoryLedger.AsNoTracking()
            .Where(e => branchIds.Contains(e.BranchId))
            .GroupBy(e => new { e.BranchId, e.ProductId, e.WarehouseId })
            .Select(g => new Pair(g.Key.BranchId, g.Key.ProductId, g.Key.WarehouseId,
                g.Sum(e => e.QtyIn - e.QtyOut), g.Sum(e => e.InValue - (e.CostAmount ?? 0m))))
            .ToListAsync(ct);
    }

    /// The value received by stock-in vouchers and the cost issued by stock-out vouchers in the period. Opening stock is
    /// neither: nothing was bought or sold to bring it in.
    private async Task<(decimal InValue, decimal OutCost)> LoadPeriodMovementAsync(
        ExecutiveScope scope, ExecutivePeriod period, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        var start = period.Start;
        var end = period.End;
        var totals = await _db.InventoryLedger.AsNoTracking()
            .Where(e => branchIds.Contains(e.BranchId) && e.PostedAt >= start && e.PostedAt < end)
            .GroupBy(e => e.SourceType)
            .Select(g => new { SourceType = g.Key, InValue = g.Sum(e => e.InValue), Cost = g.Sum(e => e.CostAmount ?? 0m) })
            .ToListAsync(ct);
        return (totals.Where(t => t.SourceType == LedgerSourceType.StockIn).Sum(t => t.InValue),
            totals.Where(t => t.SourceType == LedgerSourceType.StockOut).Sum(t => t.Cost));
    }

    /// The product × warehouse pairs below zero.
    private static IEnumerable<BalanceRow> BelowZero(IEnumerable<BalanceRow> balances) =>
        balances.Where(b => b.Quantity < 0m);

    private sealed record BalanceRow(Guid BranchId, Guid ProductId, Guid WarehouseId, decimal Quantity);

    /// Quantity and Value come from the ledger.
    private sealed record Pair(Guid BranchId, Guid ProductId, Guid WarehouseId, decimal Quantity, decimal Value);
}
