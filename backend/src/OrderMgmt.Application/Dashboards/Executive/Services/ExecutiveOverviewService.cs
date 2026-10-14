using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// The cross-domain parts of the executive dashboard: the branch comparison, the attention list and the documents
/// created today. Each part of a domain is read only when the user may read that domain's report, through the metrics
/// service of the domain, inside one snapshot.
public class ExecutiveOverviewService : IExecutiveOverviewService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;
    private readonly ISalesMetricsService _sales;
    private readonly ICashMetricsService _cash;
    private readonly IDebtMetricsService _debt;
    private readonly IInventoryMetricsService _inventory;

    public ExecutiveOverviewService(IAppDbContext db, ICurrentUser currentUser, IExecutiveScopeResolver scopes,
        ExecutiveLookups lookups, IDateTime clock, ITransactionRunner transaction, ISalesMetricsService sales,
        ICashMetricsService cash, IDebtMetricsService debt, IInventoryMetricsService inventory)
    {
        _db = db;
        _currentUser = currentUser;
        _scopes = scopes;
        _lookups = lookups;
        _clock = clock;
        _transaction = transaction;
        _sales = sales;
        _cash = cash;
        _debt = debt;
        _inventory = inventory;
    }

    public Task<List<BranchComparisonRowDto>> GetBranchComparisonAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildBranchComparisonAsync(query, c), ct);

    private async Task<List<BranchComparisonRowDto>> BuildBranchComparisonAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var at = query.At ?? today;
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);

        // A domain the user may not see is not read at all; a branch missing from a domain's answer has 0 there.
        var sales = _currentUser.HasPermission(Permissions.Reports.Profit)
            ? await _sales.GetByBranchAsync(scope, period, ct)
            : null;
        var receivable = _currentUser.HasPermission(Permissions.Reports.Debt)
            ? await _debt.GetReceivableByBranchAsync(scope, at, ct)
            : null;
        var stockValue = _currentUser.HasPermission(Permissions.Reports.Inventory) && canViewCost
            ? await _inventory.GetValueByBranchAsync(scope, ct)
            : null;
        var branches = await _lookups.BranchesAsync(scope.BranchIds, ct);

        return scope.BranchIds
            .Select(id =>
            {
                var totals = sales is null
                    ? null
                    : sales.TryGetValue(id, out var found) ? found : new SalesTotals(0m, 0m);
                var branch = branches.TryGetValue(id, out var known) ? known : (Code: string.Empty, Name: string.Empty);
                return new BranchComparisonRowDto
                {
                    BranchId = id,
                    BranchCode = branch.Code,
                    BranchName = branch.Name,
                    Revenue = totals?.Revenue,
                    MarginPct = totals is not null && canViewCost
                        ? ExecutiveMath.MarginPct(totals.Revenue, totals.Revenue - totals.Cost)
                        : null,
                    Receivable = receivable?.GetValueOrDefault(id),
                    StockValue = stockValue?.GetValueOrDefault(id),
                };
            })
            .OrderBy(r => r.BranchCode, StringComparer.Ordinal)
            .ThenBy(r => r.BranchId)
            .ToList();
    }

    public Task<AttentionDto> GetAttentionAsync(ExecutiveQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildAttentionAsync(query, c), ct);

    private async Task<AttentionDto> BuildAttentionAsync(ExecutiveQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var at = query.At ?? today;
        var attention = new AttentionDto { At = at };

        if (_currentUser.HasPermission(Permissions.Reports.Debt))
        {
            var debt = await _debt.GetAttentionAsync(scope, at, ct);
            attention.OverdueReceivables = new AttentionItemDto { Count = debt.OverduePartners, Amount = debt.OverdueAmount };
            attention.OverCreditLimit = new AttentionItemDto { Count = debt.OverLimitCount, Amount = debt.OverLimitAmount };
            attention.PayablesDueSoon = new AttentionItemDto
            {
                Count = debt.PayablesDueSoonCount,
                Amount = debt.PayablesDueSoonAmount,
            };
        }

        // Stock has a count and no amount: the pairs below zero are not worth a sum.
        if (_currentUser.HasPermission(Permissions.Reports.Inventory))
            attention.NegativeStock = new AttentionItemDto { Count = await _inventory.CountNegativeStockAsync(scope, ct) };

        if (_currentUser.HasPermission(Permissions.Reports.Cash))
        {
            var funds = await _cash.GetNegativeFundsAsync(scope, at, ct);
            attention.NegativeFunds = new AttentionItemDto { Count = funds.Count, Amount = funds.Amount };
        }

        return attention;
    }

    public Task<List<RecentDocumentDto>> GetRecentAsync(RecentQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildRecentAsync(query, c), ct);

    private async Task<List<RecentDocumentDto>> BuildRecentAsync(RecentQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var startOfToday = VnTime.ToUtc(VnTime.StartOfDay(today));

        var rows = new List<RecentRow>();
        if (_currentUser.HasPermission(Permissions.Reports.Cash))
            rows.AddRange(await LoadRecentCashAsync(scope, startOfToday, query.Limit, ct));
        if (_currentUser.HasPermission(Permissions.Reports.Inventory))
            rows.AddRange(await LoadRecentStockAsync(scope, startOfToday, query.Limit, ct));

        var latest = rows
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .Take(query.Limit)
            .ToList();
        var users = await _lookups.UserNamesAsync(latest.Select(r => r.OwnerUserId).Distinct().ToList(), ct);
        var branchCodes = await _lookups.BranchCodesAsync(latest.Select(r => r.BranchId).Distinct().ToList(), ct);

        return latest
            .Select(r => new RecentDocumentDto
            {
                Kind = r.Kind,
                Id = r.Id,
                Code = r.Code,
                CreatedAt = r.CreatedAt,
                BranchCode = branchCodes.TryGetValue(r.BranchId, out var code) ? code : string.Empty,
                PartnerName = r.PartnerName,
                OwnerName = users.TryGetValue(r.OwnerUserId, out var name) ? name : null,
                Amount = r.Amount,
            })
            .ToList();
    }

    /// Receipts and payments created today, active and not deleted. An automatic voucher is left out: its stock voucher
    /// is the document the user created. A voucher without a partner shows the person who paid or was paid.
    private async Task<List<RecentRow>> LoadRecentCashAsync(ExecutiveScope scope, DateTimeOffset startOfToday, int limit,
        CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        var vouchers = await _db.CashVouchers.AsNoTracking()
            .Where(v => v.Status == DocumentStatus.Active && v.SourceStockVoucherId == null
                && branchIds.Contains(v.BranchId) && v.CreatedAt >= startOfToday)
            .OrderByDescending(v => v.CreatedAt)
            .ThenBy(v => v.Code)
            .ThenBy(v => v.Id)
            .Take(limit)
            .Select(v => new
            {
                v.Type, v.Id, v.Code, v.CreatedAt, v.BranchId, PartnerName = v.PartnerName ?? v.PersonName,
                v.OwnerUserId, v.Total,
            })
            .ToListAsync(ct);
        return vouchers
            .Select(v => new RecentRow(KindOf(v.Type), v.Id, v.Code, v.CreatedAt, v.BranchId, v.PartnerName,
                v.OwnerUserId, v.Total))
            .ToList();
    }

    /// Stock-in and stock-out vouchers created today, active and not deleted.
    private async Task<List<RecentRow>> LoadRecentStockAsync(ExecutiveScope scope, DateTimeOffset startOfToday, int limit,
        CancellationToken ct)
    {
        var branchIds = scope.BranchIds;
        var vouchers = await _db.StockVouchers.AsNoTracking()
            .Where(v => v.Status == StockVoucherStatus.Active && branchIds.Contains(v.BranchId)
                && v.CreatedAt >= startOfToday)
            .OrderByDescending(v => v.CreatedAt)
            .ThenBy(v => v.Code)
            .ThenBy(v => v.Id)
            .Take(limit)
            .Select(v => new { v.Type, v.Id, v.Code, v.CreatedAt, v.BranchId, v.PartnerName, v.OwnerUserId, v.Total })
            .ToListAsync(ct);
        return vouchers
            .Select(v => new RecentRow(KindOf(v.Type), v.Id, v.Code, v.CreatedAt, v.BranchId, v.PartnerName,
                v.OwnerUserId, v.Total))
            .ToList();
    }

    private static RecentDocumentKind KindOf(CashVoucherType type) => type switch
    {
        CashVoucherType.Receipt => RecentDocumentKind.Receipt,
        CashVoucherType.Payment => RecentDocumentKind.Payment,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown cash voucher type."),
    };

    private static RecentDocumentKind KindOf(StockDirection type) => type switch
    {
        StockDirection.In => RecentDocumentKind.StockIn,
        StockDirection.Out => RecentDocumentKind.StockOut,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown stock voucher type."),
    };

    private sealed record RecentRow(RecentDocumentKind Kind, Guid Id, string Code, DateTimeOffset CreatedAt, Guid BranchId,
        string? PartnerName, Guid OwnerUserId, decimal Amount);
}
