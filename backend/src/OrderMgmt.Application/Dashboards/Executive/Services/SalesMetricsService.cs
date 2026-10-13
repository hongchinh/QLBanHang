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
/// stock-in voucher with NTL (P3); revenue is before VAT and freight is not revenue (P4).
public class SalesMetricsService : ISalesMetricsService
{
    private readonly IAppDbContext _db;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public SalesMetricsService(IAppDbContext db, IExecutiveScopeResolver scopes, IDateTime clock,
        ITransactionRunner transaction)
    {
        _db = db;
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

        // One read for the period and the one before it; they are adjacent, so an instant tells them apart.
        var vouchers = await LoadVouchersAsync(scope, period.PrevStart, period.End, ct);
        var revenue = vouchers.Where(v => v.At >= period.Start).Sum(v => v.Revenue);
        var prevRevenue = vouchers.Where(v => v.At < period.Start).Sum(v => v.Revenue);

        return new SalesSummaryDto
        {
            From = period.From,
            To = period.To,
            PrevFrom = period.PrevFrom,
            PrevTo = period.PrevTo,
            Revenue = revenue,
            PrevRevenue = prevRevenue,
            RevenueDeltaPct = ExecutiveMath.DeltaPct(revenue, prevRevenue),
        };
    }

    /// The counted vouchers of the scope with VoucherAt in [from, to): Active, not deleted, reason XBH (+) or NTL (−).
    private async Task<List<VoucherRow>> LoadVouchersAsync(
        ExecutiveScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // The reason ids come from their own query: IgnoreQueryFilters would also reveal deleted vouchers here.
        var reasons = await _db.StockReasons.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.IsSystem && (r.Code == StockReasonCodes.Sale || r.Code == StockReasonCodes.SaleReturn))
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(ct);
        var saleIds = reasons.Where(r => r.Code == StockReasonCodes.Sale).Select(r => r.Id).ToList();
        var returnIds = reasons.Where(r => r.Code == StockReasonCodes.SaleReturn).Select(r => r.Id).ToList();
        var branchIds = scope.BranchIds;

        var rows = await _db.StockVouchers.AsNoTracking()
            .Where(v => v.Status == StockVoucherStatus.Active
                && branchIds.Contains(v.BranchId)
                && (saleIds.Contains(v.ReasonId) || returnIds.Contains(v.ReasonId))
                && v.VoucherAt >= from && v.VoucherAt < to)
            .Select(v => new
            {
                v.Id,
                v.VoucherAt,
                IsReturn = returnIds.Contains(v.ReasonId),
                Revenue = v.Lines.Sum(l => l.Amount - l.DiscountAmount - l.OrderDiscountAllocated
                    - (v.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m)),
            })
            .ToListAsync(ct);

        return rows.Select(r => new VoucherRow(r.Id, r.VoucherAt, r.IsReturn ? -r.Revenue : r.Revenue)).ToList();
    }

    /// Revenue is signed: a return counts negative.
    private sealed record VoucherRow(Guid VoucherId, DateTimeOffset At, decimal Revenue);
}
