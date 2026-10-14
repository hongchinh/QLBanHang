using Microsoft.EntityFrameworkCore;
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
    private const int TopDebtorCount = 10;
    private const int PayablesDueDays = 30;
    private const int PayablesDueMaxRows = 50;

    private readonly IAppDbContext _db;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public DebtMetricsService(IAppDbContext db, IExecutiveScopeResolver scopes, ExecutiveLookups lookups,
        IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _scopes = scopes;
        _lookups = lookups;
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
        var atEnd = ExecutiveDay.End(at);
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

    public Task<DebtBreakdownDto> GetBreakdownAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildBreakdownAsync(query, c), ct);

    private async Task<DebtBreakdownDto> BuildBreakdownAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var at = query.At ?? today;
        var atEnd = ExecutiveDay.End(at);

        var receivable = await LoadOpenItemsAsync(scope, DebtSide.Receivable, atEnd, ct);
        var payable = await LoadOpenItemsAsync(scope, DebtSide.Payable, atEnd, ct);
        var limits = await LoadLimitsAsync(scope, ct);

        var debtors = DebtorBalances(receivable, at);
        var breaches = LimitBreaches(receivable, limits);
        var employees = EmployeeTotals(receivable, at);
        var payablesDue = SoonestPayables(payable, at);

        // Names of everything the four lists show, each from its own lookup by id.
        var partners = await _lookups.PartnersAsync(
            debtors.Select(d => d.PartnerId)
                .Concat(breaches.Select(b => b.PartnerId))
                .Concat(payablesDue.Select(i => i.PartnerId))
                .Distinct().ToList(), ct);
        var users = await _lookups.UserNamesAsync(
            employees.Where(e => e.OwnerUserId is not null).Select(e => e.OwnerUserId!.Value).ToList(), ct);
        var branchCodes = await _lookups.BranchCodesAsync(
            breaches.Select(b => b.BranchId).Concat(payablesDue.Select(i => i.BranchId)).Distinct().ToList(), ct);

        return new DebtBreakdownDto
        {
            At = at,
            TopDebtors = TopDebtorRows(debtors, partners),
            OverLimit = OverLimitRows(breaches, partners, branchCodes),
            ByEmployee = ByEmployeeRows(employees, users),
            PayablesDue = PayableDueRows(payablesDue, partners, branchCodes),
        };
    }

    /// The receivable balance of each branch of the scope at the end of the given day. Runs inside the caller's
    /// snapshot.
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetReceivableByBranchAsync(ExecutiveScope scope, DateOnly at,
        CancellationToken ct = default)
    {
        var receivable = await LoadOpenItemsAsync(scope, DebtSide.Receivable, ExecutiveDay.End(at), ct);
        return receivable.GroupBy(i => i.BranchId).ToDictionary(g => g.Key, g => DebtPositionMath.Balance(g));
    }

    /// What needs action at the end of the given day: the overdue receivables, the credit limits exceeded and the
    /// payables that fall due within the next seven days. Runs inside the caller's snapshot.
    public async Task<DebtAttention> GetAttentionAsync(ExecutiveScope scope, DateOnly at, CancellationToken ct = default)
    {
        var atEnd = ExecutiveDay.End(at);
        var receivable = await LoadOpenItemsAsync(scope, DebtSide.Receivable, atEnd, ct);
        var payable = await LoadOpenItemsAsync(scope, DebtSide.Payable, atEnd, ct);
        var limits = await LoadLimitsAsync(scope, ct);

        var receivableTotals = DebtPositionMath.Totals(receivable, at, DebtAging.DefaultBoundaries);
        var payableTotals = DebtPositionMath.Totals(payable, at, DebtAging.DefaultBoundaries);
        var breaches = LimitBreaches(receivable, limits);
        return new DebtAttention(receivableTotals.OverduePartnerCount, receivableTotals.Overdue,
            breaches.Count, breaches.Sum(b => b.Over), payableTotals.DueSoonCount, payableTotals.DueSoon);
    }

    /// The receivable balance of each customer over every branch of the scope; customers who do not owe are left out.
    private static List<DebtorBalance> DebtorBalances(IEnumerable<OpenDebtItem> receivable, DateOnly at) =>
        receivable
            .GroupBy(i => i.PartnerId)
            .Select(g => new DebtorBalance(g.Key, DebtPositionMath.Balance(g),
                g.Where(i => DebtPositionMath.IsOverdue(i, at)).Sum(i => i.Open)))
            .Where(d => d.Balance > 0m)
            .ToList();

    /// Each credit limit against the receivable balance of its own branch and customer; only a balance above the limit
    /// is a breach.
    private static List<LimitBreach> LimitBreaches(IEnumerable<OpenDebtItem> receivable, IEnumerable<CreditLimitRow> limits)
    {
        var balances = receivable
            .GroupBy(i => (i.BranchId, i.PartnerId))
            .ToDictionary(g => g.Key, g => DebtPositionMath.Balance(g));
        return limits
            .Select(l => new LimitBreach(l.CustomerId, l.BranchId,
                balances.GetValueOrDefault((l.BranchId, l.CustomerId)), l.Amount))
            .Where(b => b.Balance > b.Limit)
            .ToList();
    }

    /// What each owner of the receivable documents is still owed and how much of it is overdue. Only documents count:
    /// unmatched money has no owner's name on it. Documents without an owner make one row.
    private static List<EmployeeTotal> EmployeeTotals(IEnumerable<OpenDebtItem> receivable, DateOnly at) =>
        receivable
            .Where(i => i.Direction == DebtDirection.Increase)
            .GroupBy(i => i.OwnerUserId)
            .Select(g => new EmployeeTotal(g.Key, g.Sum(i => i.Open),
                g.Where(i => DebtPositionMath.IsOverdue(i, at)).Sum(i => i.Open)))
            .ToList();

    /// The payable documents that fall due from the given day up to 30 days after it, the earliest first, at most 50.
    /// The ids end the order, so equal due dates and codes of two branches still come back in the same order.
    private static List<OpenDebtItem> SoonestPayables(IEnumerable<OpenDebtItem> payable, DateOnly at) =>
        payable
            .Where(i => DebtPositionMath.IsDueWithin(i, at, PayablesDueDays))
            .OrderBy(i => i.Due)
            .ThenBy(i => i.DocCode, StringComparer.Ordinal)
            .ThenBy(i => i.BranchId)
            .ThenBy(i => i.SourceId)
            .Take(PayablesDueMaxRows)
            .ToList();

    private static List<DebtPartnerRowDto> TopDebtorRows(IEnumerable<DebtorBalance> debtors,
        IReadOnlyDictionary<Guid, (string Code, string Name)> partners) =>
        debtors
            .Select(d =>
            {
                var (code, name) = PartnerNameOf(partners, d.PartnerId);
                return new DebtPartnerRowDto
                {
                    PartnerId = d.PartnerId,
                    PartnerCode = code,
                    PartnerName = name,
                    Balance = d.Balance,
                    Overdue = d.Overdue,
                };
            })
            .OrderByDescending(r => r.Balance)
            .ThenBy(r => r.PartnerCode, StringComparer.Ordinal)
            .ThenBy(r => r.PartnerId)
            .Take(TopDebtorCount)
            .ToList();

    private static List<CreditLimitBreachDto> OverLimitRows(IEnumerable<LimitBreach> breaches,
        IReadOnlyDictionary<Guid, (string Code, string Name)> partners, IReadOnlyDictionary<Guid, string> branchCodes) =>
        breaches
            .Select(b =>
            {
                var (code, name) = PartnerNameOf(partners, b.PartnerId);
                return new CreditLimitBreachDto
                {
                    PartnerId = b.PartnerId,
                    PartnerCode = code,
                    PartnerName = name,
                    BranchId = b.BranchId,
                    BranchCode = BranchCodeOf(branchCodes, b.BranchId),
                    Balance = b.Balance,
                    Limit = b.Limit,
                    Over = b.Over,
                };
            })
            .OrderByDescending(r => r.Over)
            .ThenBy(r => r.PartnerCode, StringComparer.Ordinal)
            .ThenBy(r => r.BranchCode, StringComparer.Ordinal)
            .ThenBy(r => r.PartnerId)
            .ToList();

    /// The largest amount owed first; the row of documents without an owner is last, whatever its amount.
    private static List<DebtEmployeeRowDto> ByEmployeeRows(IEnumerable<EmployeeTotal> employees,
        IReadOnlyDictionary<Guid, string> users) =>
        employees
            .Select(e => new DebtEmployeeRowDto
            {
                OwnerUserId = e.OwnerUserId,
                OwnerName = e.OwnerUserId is { } id && users.TryGetValue(id, out var name) ? name : null,
                Open = e.Open,
                Overdue = e.Overdue,
            })
            .OrderBy(r => r.OwnerUserId is null ? 1 : 0)
            .ThenByDescending(r => r.Open)
            .ThenBy(r => r.OwnerName, StringComparer.Ordinal)
            .ThenBy(r => r.OwnerUserId)
            .ToList();

    private static List<PayableDueRowDto> PayableDueRows(IEnumerable<OpenDebtItem> items,
        IReadOnlyDictionary<Guid, (string Code, string Name)> partners, IReadOnlyDictionary<Guid, string> branchCodes) =>
        items
            .Select(i =>
            {
                var (code, name) = PartnerNameOf(partners, i.PartnerId);
                return new PayableDueRowDto
                {
                    PartnerId = i.PartnerId,
                    PartnerCode = code,
                    PartnerName = name,
                    BranchId = i.BranchId,
                    BranchCode = BranchCodeOf(branchCodes, i.BranchId),
                    SourceType = i.SourceType,
                    SourceId = i.SourceId,
                    DocCode = i.DocCode,
                    DueDate = i.Due,
                    Open = i.Open,
                };
            })
            .ToList();

    private static (string? Code, string? Name) PartnerNameOf(
        IReadOnlyDictionary<Guid, (string Code, string Name)> partners, Guid id) =>
        partners.TryGetValue(id, out var partner) ? (partner.Code, partner.Name) : (null, null);

    private static string BranchCodeOf(IReadOnlyDictionary<Guid, string> branchCodes, Guid id) =>
        branchCodes.TryGetValue(id, out var code) ? code : string.Empty;

    /// The credit limits of the branches of the scope (a deleted limit is no limit).
    private async Task<List<CreditLimitRow>> LoadLimitsAsync(ExecutiveScope scope, CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        return await _db.CreditLimits.AsNoTracking()
            .Where(l => branchIds.Contains(l.BranchId))
            .Select(l => new CreditLimitRow(l.CustomerId, l.BranchId, l.Amount))
            .ToListAsync(ct);
    }

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

    private sealed record DebtorBalance(Guid PartnerId, decimal Balance, decimal Overdue);

    private sealed record CreditLimitRow(Guid CustomerId, Guid BranchId, decimal Amount);

    private sealed record LimitBreach(Guid PartnerId, Guid BranchId, decimal Balance, decimal Limit)
    {
        /// How far the balance is above the limit.
        public decimal Over => Balance - Limit;
    }

    private sealed record EmployeeTotal(Guid? OwnerUserId, decimal Open, decimal Overdue);
}
