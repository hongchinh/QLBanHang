using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Guards;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Cash of the executive dashboard, read from the fund movements the cash book reads, so cancelled and deleted
/// documents are out and automatic vouchers are in. A receipt or a payment is a movement of that kind; fund transfers
/// and opening amounts are neither. Balances are as at a date and count every fund of the scope, inactive ones too.
public class CashMetricsService : ICashMetricsService
{
    private const int LargestCount = 10;

    private readonly IAppDbContext _db;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public CashMetricsService(IAppDbContext db, IExecutiveScopeResolver scopes, ExecutiveLookups lookups,
        IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _scopes = scopes;
        _lookups = lookups;
        _clock = clock;
        _transaction = transaction;
    }

    public Task<CashSummaryDto> GetSummaryAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSummaryAsync(query, c), ct);

    private async Task<CashSummaryDto> BuildSummaryAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var at = query.At ?? today;

        var funds = await LoadFundsAsync(scope, ct);
        var movements = await LoadMovementsAsync(funds, ct);
        var branchCodes = await _lookups.BranchCodesAsync(scope.BranchIds, ct);

        var current = InWindow(movements, period.Start, period.End);
        var previous = InWindow(movements, period.PrevStart, period.PrevEnd);
        var receipts = ReceiptTotal(current);
        var payments = PaymentTotal(current);
        var net = receipts - payments;
        var prevNet = ReceiptTotal(previous) - PaymentTotal(previous);

        var balances = BalancesAt(movements, ExecutiveDay.End(at));
        var rows = funds
            .Select(f => new FundBalanceDto
            {
                FundId = f.Id,
                BranchId = f.BranchId,
                BranchCode = branchCodes.TryGetValue(f.BranchId, out var branchCode) ? branchCode : string.Empty,
                Code = f.Code,
                Name = f.Name,
                Kind = f.Kind,
                IsActive = f.IsActive,
                Balance = balances.GetValueOrDefault(f.Id),
            })
            .OrderBy(f => f.BranchCode, StringComparer.Ordinal)
            .ThenBy(f => f.Kind)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();

        return new CashSummaryDto
        {
            From = period.From,
            To = period.To,
            PrevFrom = period.PrevFrom,
            PrevTo = period.PrevTo,
            At = at,
            Receipts = receipts,
            Payments = payments,
            Net = net,
            PrevNet = prevNet,
            NetDeltaPct = ExecutiveMath.DeltaPct(net, prevNet),
            Balance = rows.Sum(f => f.Balance),
            CashBalance = rows.Where(f => f.Kind == FundKind.Cash).Sum(f => f.Balance),
            BankBalance = rows.Where(f => f.Kind == FundKind.Bank).Sum(f => f.Balance),
            Funds = rows,
        };
    }

    public Task<CashSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSeriesAsync(query, c), ct);

    private async Task<CashSeriesDto> BuildSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);

        var funds = await LoadFundsAsync(scope, ct);
        var movements = await LoadMovementsAsync(funds, ct);
        // Only the period's movements are grouped: a week or a month may reach past the period's first or last day.
        var totals = InWindow(movements, period.Start, period.End)
            .GroupBy(m => ExecutiveSeries.BucketOf(VnTime.ToVnDate(m.At), query.Granularity))
            .ToDictionary(g => g.Key, g => (Receipts: ReceiptTotal(g), Payments: PaymentTotal(g)));

        return new CashSeriesDto
        {
            Granularity = query.Granularity,
            Points = ExecutiveSeries.Buckets(period.From, period.To, query.Granularity)
                .Select(bucket =>
                {
                    totals.TryGetValue(bucket, out var total);
                    return new CashPointDto { Date = bucket, Receipts = total.Receipts, Payments = total.Payments };
                })
                .ToList(),
        };
    }

    public Task<CashBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildBreakdownAsync(query, c), ct);

    private async Task<CashBreakdownDto> BuildBreakdownAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);

        var funds = await LoadFundsAsync(scope, ct);
        var movements = await LoadMovementsAsync(funds, ct);
        var branchCodes = await _lookups.BranchCodesAsync(scope.BranchIds, ct);

        return new CashBreakdownDto
        {
            ByReason = await ReasonTotalsAsync(funds, period, ct),
            Largest = LargestDocuments(InWindow(movements, period.Start, period.End), funds, branchCodes),
        };
    }

    /// The vouchers of the period by reason, read from the voucher table: Active, not deleted, in a fund of the scope.
    /// Receipts come first, then payments; inside each the largest total first.
    private async Task<List<CashReasonTotalDto>> ReasonTotalsAsync(
        IReadOnlyCollection<FundRow> funds, ExecutivePeriod period, CancellationToken ct)
    {
        var fundIds = funds.Select(f => f.Id).ToList();
        var totals = await _db.CashVouchers.AsNoTracking()
            .Where(v => v.Status == DocumentStatus.Active && fundIds.Contains(v.FundId)
                && v.VoucherAt >= period.Start && v.VoucherAt < period.End)
            .GroupBy(v => new { v.ReasonId, v.Type })
            .Select(g => new { g.Key.ReasonId, g.Key.Type, Total = g.Sum(v => v.Total), Count = g.Count() })
            .ToListAsync(ct);

        // The names come from their own query: IgnoreQueryFilters would also reveal deleted vouchers in the one above,
        // and a reason deleted after use keeps its name in the history.
        var reasonIds = totals.Select(t => t.ReasonId).ToList();
        var reasons = await _db.CashReasons.IgnoreQueryFilters().AsNoTracking()
            .Where(r => reasonIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Code, r.Name })
            .ToDictionaryAsync(r => r.Id, ct);

        return totals
            .Select(t =>
            {
                reasons.TryGetValue(t.ReasonId, out var reason);
                return new CashReasonTotalDto
                {
                    ReasonId = t.ReasonId,
                    ReasonCode = reason?.Code,
                    ReasonName = reason?.Name,
                    Type = t.Type,
                    Total = t.Total,
                    Count = t.Count,
                };
            })
            .OrderBy(r => r.Type == CashVoucherType.Receipt ? 0 : 1)
            .ThenByDescending(r => r.Total)
            .ThenBy(r => r.ReasonCode, StringComparer.Ordinal)
            .ThenBy(r => r.ReasonId)
            .ToList();
    }

    /// The largest receipts and payments by amount, then date, then code. The id ends the order, so two documents of
    /// different branches with the same code, date and amount still come back in the same order.
    private static List<CashDocumentDto> LargestDocuments(IEnumerable<FundMovement> movements,
        IReadOnlyCollection<FundRow> funds, IReadOnlyDictionary<Guid, string> branchCodes)
    {
        var branchOfFund = funds.ToDictionary(f => f.Id, f => f.BranchId);
        return movements
            .Where(m => m.Kind is FundMovementKind.Receipt or FundMovementKind.Payment)
            .OrderByDescending(m => Math.Abs(m.Delta))
            .ThenBy(m => m.At)
            .ThenBy(m => m.Code, StringComparer.Ordinal)
            .ThenBy(m => m.SourceId)
            .Take(LargestCount)
            .Select(m => new CashDocumentDto
            {
                Id = m.SourceId,
                Type = TypeOf(m.Kind),
                Code = m.Code,
                At = m.At,
                BranchCode = branchOfFund.TryGetValue(m.FundId, out var branchId)
                    && branchCodes.TryGetValue(branchId, out var branchCode) ? branchCode : string.Empty,
                PartnerOrPerson = m.PartnerOrPerson,
                Description = m.Description,
                Total = Math.Abs(m.Delta),
            })
            .ToList();
    }

    private static CashVoucherType TypeOf(FundMovementKind kind) => kind switch
    {
        FundMovementKind.Receipt => CashVoucherType.Receipt,
        FundMovementKind.Payment => CashVoucherType.Payment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only receipts and payments are documents."),
    };

    /// The funds of the scope, active or not: an inactive fund keeps its history and its balance.
    private async Task<List<FundRow>> LoadFundsAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.Funds.AsNoTracking()
            .Where(f => branchIds.Contains(f.BranchId))
            .Select(f => new FundRow(f.Id, f.BranchId, f.Code, f.Name, f.Kind, f.IsActive))
            .ToListAsync(ct);
    }

    /// Every movement of the given funds, the opening amounts included. Without a fund there is nothing to read.
    private async Task<List<FundMovement>> LoadMovementsAsync(IReadOnlyCollection<FundRow> funds, CancellationToken ct)
    {
        if (funds.Count == 0)
            return new List<FundMovement>();

        var byFund = await FundMovementQuery.LoadAsync(_db, funds.Select(f => f.Id).ToList(), ct);
        return byFund.Values.SelectMany(m => m).ToList();
    }

    /// The balance of each fund at an instant: its opening amount, whatever its date, and every movement before the
    /// instant. A fund without a movement has no entry.
    private static Dictionary<Guid, decimal> BalancesAt(IEnumerable<FundMovement> movements, DateTimeOffset atEnd) =>
        movements
            .Where(m => m.Kind == FundMovementKind.Opening || m.At < atEnd)
            .GroupBy(m => m.FundId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Delta));

    /// The movements dated in [from, to).
    private static List<FundMovement> InWindow(IEnumerable<FundMovement> movements, DateTimeOffset from, DateTimeOffset to) =>
        movements.Where(m => m.At >= from && m.At < to).ToList();

    private static decimal ReceiptTotal(IEnumerable<FundMovement> movements) =>
        movements.Where(m => m.Kind == FundMovementKind.Receipt).Sum(m => m.Delta);

    /// Payments are negative movements; the total is positive.
    private static decimal PaymentTotal(IEnumerable<FundMovement> movements) =>
        movements.Where(m => m.Kind == FundMovementKind.Payment).Sum(m => -m.Delta);

    private sealed record FundRow(Guid Id, Guid BranchId, string Code, string Name, FundKind Kind, bool IsActive);
}
