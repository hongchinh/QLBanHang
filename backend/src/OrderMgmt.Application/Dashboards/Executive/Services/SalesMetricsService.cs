using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Sales of the executive dashboard. A sale is a stock-out voucher with the system reason XBH and a return is a
/// stock-in voucher with NTL (P3); revenue is before VAT and freight is not revenue (P4). The cost of a sale is the
/// cost of the goods it took out of stock and a return gives back the value at which the goods re-enter it (P5).
public class SalesMetricsService : ISalesMetricsService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public SalesMetricsService(IAppDbContext db, ICurrentUser currentUser, IExecutiveScopeResolver scopes,
        IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _currentUser = currentUser;
        _scopes = scopes;
        _clock = clock;
        _transaction = transaction;
    }

    public Task<SalesSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSummaryAsync(query, c), ct);

    private async Task<SalesSummaryDto> BuildSummaryAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);
        var settings = await _db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);

        // One read for the period and the one before it; they are adjacent, so an instant tells them apart.
        var vouchers = await LoadVouchersAsync(scope, period.PrevStart, period.End, canViewCost, ct);
        var current = vouchers.Where(v => v.At >= period.Start).ToList();
        var previous = vouchers.Where(v => v.At < period.Start).ToList();
        var revenue = current.Sum(v => v.Revenue);
        var prevRevenue = previous.Sum(v => v.Revenue);
        var cost = current.Sum(v => v.Cost);
        var grossProfit = revenue - cost;
        var prevGrossProfit = prevRevenue - previous.Sum(v => v.Cost);

        return new SalesSummaryDto
        {
            From = period.From,
            To = period.To,
            PrevFrom = period.PrevFrom,
            PrevTo = period.PrevTo,
            Revenue = revenue,
            PrevRevenue = prevRevenue,
            RevenueDeltaPct = ExecutiveMath.DeltaPct(revenue, prevRevenue),
            CanViewCost = canViewCost,
            Cost = canViewCost ? cost : null,
            GrossProfit = canViewCost ? grossProfit : null,
            MarginPct = canViewCost ? ExecutiveMath.MarginPct(revenue, grossProfit) : null,
            PrevMarginPct = canViewCost ? ExecutiveMath.MarginPct(prevRevenue, prevGrossProfit) : null,
            IsProvisional = CostProvisional.IsProvisional(period.To, settings.CostingPeriod, today),
        };
    }

    /// The counted vouchers of the scope with VoucherAt in [from, to): Active, not deleted, reason XBH (+) or NTL (−).
    /// Costs are read only when the user may see them.
    private async Task<List<VoucherRow>> LoadVouchersAsync(
        ExecutiveScope scope, DateTimeOffset from, DateTimeOffset to, bool withCost, CancellationToken ct)
    {
        // The reason ids come from their own query: IgnoreQueryFilters would also reveal deleted vouchers here.
        var reasons = await _db.StockReasons.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.IsSystem && (r.Code == StockReasonCodes.Sale || r.Code == StockReasonCodes.SaleReturn))
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(ct);
        var saleIds = reasons.Where(r => r.Code == StockReasonCodes.Sale).Select(r => r.Id).ToList();
        var returnIds = reasons.Where(r => r.Code == StockReasonCodes.SaleReturn).Select(r => r.Id).ToList();
        var branchIds = scope.BranchIds;

        var vouchers = _db.StockVouchers.AsNoTracking()
            .Where(v => v.Status == StockVoucherStatus.Active
                && branchIds.Contains(v.BranchId)
                && (saleIds.Contains(v.ReasonId) || returnIds.Contains(v.ReasonId))
                && v.VoucherAt >= from && v.VoucherAt < to);

        var rows = await vouchers
            .Select(v => new
            {
                v.Id,
                v.VoucherAt,
                IsReturn = returnIds.Contains(v.ReasonId),
                Revenue = v.Lines.Sum(l => l.Amount - l.DiscountAmount - l.OrderDiscountAllocated
                    - (v.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m)),
            })
            .ToListAsync(ct);

        // The ledger holds the rows of Active, non-deleted vouchers only; costs are written when a voucher is posted.
        var costs = withCost
            ? await _db.InventoryLedger.AsNoTracking()
                .Where(e => (e.SourceType == LedgerSourceType.StockOut || e.SourceType == LedgerSourceType.StockIn)
                    && vouchers.Any(v => v.Id == e.SourceId))
                .GroupBy(e => e.SourceId)
                .Select(g => new
                {
                    VoucherId = g.Key,
                    CostOut = g.Sum(e => e.CostAmount ?? 0m),
                    ValueIn = g.Sum(e => e.InValue),
                })
                .ToDictionaryAsync(c => c.VoucherId, ct)
            : null;

        return rows.Select(r =>
        {
            var sign = r.IsReturn ? -1m : 1m;
            var cost = costs is not null && costs.TryGetValue(r.Id, out var c) ? (r.IsReturn ? c.ValueIn : c.CostOut) : 0m;
            return new VoucherRow(r.Id, r.VoucherAt, sign * r.Revenue, sign * cost);
        }).ToList();
    }

    /// Revenue and Cost are signed: a return counts negative.
    private sealed record VoucherRow(Guid VoucherId, DateTimeOffset At, decimal Revenue, decimal Cost);
}
