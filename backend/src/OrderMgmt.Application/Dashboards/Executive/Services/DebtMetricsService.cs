using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.CashDebt.Reports.Services;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Debt of the executive dashboard, as at a date. The open items are read through the position query the debt reports
/// read, one branch and side at a time, and every figure is computed on them in memory, so a figure equals the matching
/// report for the same branch and date. Only documents still owed (Increase items with something open) are due or
/// overdue; unmatched money (Decrease items) is the unallocated amount.
public class DebtMetricsService : IDebtMetricsService
{
    private readonly IAppDbContext _db;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public DebtMetricsService(IAppDbContext db, IExecutiveScopeResolver scopes, IDateTime clock,
        ITransactionRunner transaction)
    {
        _db = db;
        _scopes = scopes;
        _clock = clock;
        _transaction = transaction;
    }

    public Task<DebtSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSummaryAsync(query, c), ct);

    private async Task<DebtSummaryDto> BuildSummaryAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var at = query.At ?? today;
        var atEnd = VnTime.ToUtc(VnTime.StartOfNextDay(at));
        var boundaries = DebtAging.DefaultBoundaries;

        var receivable = await LoadOpenItemsAsync(scope, DebtSide.Receivable, atEnd, ct);
        var payable = await LoadOpenItemsAsync(scope, DebtSide.Payable, atEnd, ct);

        return new DebtSummaryDto
        {
            At = at,
            OverdueBuckets = DebtAging.Ranges(boundaries, firstFrom: 1)
                .Select(bucket => new AgingBucketDto { From = bucket.From, To = bucket.To })
                .ToList(),
            Receivable = SideSummary(DebtPositionMath.Totals(receivable, at, boundaries)),
            Payable = SideSummary(DebtPositionMath.Totals(payable, at, boundaries)),
        };
    }

    private static DebtSideSummaryDto SideSummary(DebtSideTotals totals) => new()
    {
        Balance = totals.Balance,
        TotalOpen = totals.TotalOpen,
        Unallocated = totals.Unallocated,
        NotDue = totals.NotDue,
        Overdue = totals.Overdue,
        OverduePartnerCount = totals.OverduePartnerCount,
        OverdueAmounts = totals.OverdueAmounts.ToList(),
        DueSoon = totals.DueSoon,
        DueSoonCount = totals.DueSoonCount,
    };

    /// The open items (something still open) of one side in every branch of the scope, as the debt reports count them
    /// at the end of the given day.
    private async Task<List<OpenDebtItem>> LoadOpenItemsAsync(
        ExecutiveScope scope, DebtSide side, DateTimeOffset atEnd, CancellationToken ct)
    {
        var items = new List<OpenDebtItem>();
        foreach (var branchId in scope.BranchIds)
        {
            var position = await DebtPositionQuery.LoadAsync(_db, branchId, side, null, atEnd, ct);
            items.AddRange(position
                .Where(p => p.Open > 0m)
                .Select(p => new OpenDebtItem(branchId, p.PartnerId, p.Direction, p.SourceType, p.SourceId, p.DocCode,
                    p.DocDate, p.DueDate, p.OwnerUserId, p.Open)));
        }

        return items;
    }
}
