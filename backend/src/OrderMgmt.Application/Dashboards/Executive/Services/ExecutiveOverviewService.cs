using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;

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
        var branches = await LoadBranchNamesAsync(scope.BranchIds, ct);

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

    /// Code and name of each branch. A deleted branch keeps them: this is a lookup by id with the query filters off.
    private async Task<Dictionary<Guid, (string Code, string Name)>> LoadBranchNamesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await _db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Code, b.Name })
            .ToDictionaryAsync(b => b.Id, b => (b.Code, b.Name), ct);
}
