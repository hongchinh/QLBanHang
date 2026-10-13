using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Interfaces;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Dashboards.Executive.Services;

/// Sales of the executive dashboard. A sale is a stock-out voucher with the system reason XBH and a return is a
/// stock-in voucher with NTL; reasons a user created never count. Revenue is before VAT and freight is not revenue.
/// The cost of a sale is the cost of the goods it took out of stock and a return gives back the value at which the
/// goods re-enter it.
public class SalesMetricsService : ISalesMetricsService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IExecutiveScopeResolver _scopes;
    private readonly ExecutiveLookups _lookups;
    private readonly IDateTime _clock;
    private readonly ITransactionRunner _transaction;

    public SalesMetricsService(IAppDbContext db, ICurrentUser currentUser, IExecutiveScopeResolver scopes,
        ExecutiveLookups lookups, IDateTime clock, ITransactionRunner transaction)
    {
        _db = db;
        _currentUser = currentUser;
        _scopes = scopes;
        _lookups = lookups;
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
        var vouchers = await LoadVouchersAsync(
            await BuildCountedQueryAsync(scope, period.PrevStart, period.End, ct), canViewCost, ct);
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

    public Task<SalesSeriesDto> GetSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildSeriesAsync(query, c), ct);

    private async Task<SalesSeriesDto> BuildSeriesAsync(ExecutiveSeriesQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);

        var vouchers = await LoadVouchersAsync(
            await BuildCountedQueryAsync(scope, period.Start, period.End, ct), canViewCost, ct);
        var totals = vouchers
            .GroupBy(v => ExecutiveSeries.BucketOf(VnTime.ToVnDate(v.At), query.Granularity))
            .ToDictionary(g => g.Key, g => new Totals(g.Sum(v => v.Revenue), g.Sum(v => v.Cost)));

        return new SalesSeriesDto
        {
            Granularity = query.Granularity,
            Points = ExecutiveSeries.Buckets(period.From, period.To, query.Granularity)
                .Select(bucket =>
                {
                    var total = totals.GetValueOrDefault(bucket) ?? new Totals(0m, 0m);
                    return new SalesPointDto
                    {
                        Date = bucket,
                        Revenue = total.Revenue,
                        Cost = canViewCost ? total.Cost : null,
                        GrossProfit = canViewCost ? total.Revenue - total.Cost : null,
                    };
                })
                .ToList(),
        };
    }

    public Task<List<SalesTopRowDto>> GetTopAsync(SalesTopQuery query, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildTopAsync(query, c), ct);

    private async Task<List<SalesTopRowDto>> BuildTopAsync(SalesTopQuery query, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var scope = await _scopes.ResolveAsync(query.BranchId, ct);
        var period = ExecutivePeriod.Create(query.From, query.To, today);
        var canViewCost = _currentUser.HasPermission(Permissions.Inventory.ViewCost);

        var counted = await BuildCountedQueryAsync(scope, period.Start, period.End, ct);
        var totals = await TotalsAsync(query.By, counted, canViewCost, ct);
        var names = await LoadNamesAsync(query.By, totals.Keys.ToList(), ct);

        var rows = totals.Select(t =>
        {
            var name = names.TryGetValue(t.Key, out var known) ? known : (Code: (string?)null, Name: string.Empty);
            return new SalesTopRowDto
            {
                Id = t.Key,
                Code = name.Code,
                Name = name.Name,
                Revenue = t.Value.Revenue,
                GrossProfit = canViewCost ? t.Value.Revenue - t.Value.Cost : null,
            };
        });
        var byGrossProfit = query.Sort == SalesTopSort.GrossProfit && canViewCost;
        // A total order: equal figures and equal names still come back in the same order, so Take(Limit) is stable.
        return rows
            .OrderByDescending(r => byGrossProfit ? r.GrossProfit!.Value : r.Revenue)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .Take(query.Limit)
            .ToList();
    }

    /// The query of the counted vouchers of the scope with VoucherAt in [from, to): Active, not deleted, reason XBH or
    /// NTL. Only the reason ids are read here; the vouchers are read when the query is run.
    private async Task<Counted> BuildCountedQueryAsync(ExecutiveScope scope, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct)
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
        return new Counted(vouchers, returnIds);
    }

    /// One row per counted voucher: a sale counts positive and a return negative, on revenue and on cost.
    /// Costs are read only when the user may see them.
    private async Task<List<VoucherRow>> LoadVouchersAsync(Counted counted, bool withCost, CancellationToken ct)
    {
        var vouchers = counted.Vouchers;
        var returnIds = counted.ReturnIds;

        var rows = await vouchers
            .Select(v => new
            {
                v.Id,
                v.VoucherAt,
                v.PartnerId,
                v.OwnerUserId,
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
            return new VoucherRow(r.Id, r.VoucherAt, r.PartnerId, r.OwnerUserId, sign * r.Revenue, sign * cost);
        }).ToList();
    }

    /// The totals of each customer, employee or product of the counted vouchers, by the grouping of the top list.
    private async Task<Dictionary<Guid, Totals>> TotalsAsync(
        SalesTopBy by, Counted counted, bool canViewCost, CancellationToken ct)
    {
        if (by == SalesTopBy.Product)
            return await TotalsByProductAsync(counted, canViewCost, ct);

        Func<VoucherRow, Guid?> key = by switch
        {
            SalesTopBy.Customer => v => v.PartnerId,
            SalesTopBy.Employee => v => v.OwnerUserId,
            _ => throw new ArgumentOutOfRangeException(nameof(by), by, "Unknown top list."),
        };
        // One read of the vouchers for both groupings, so the cost flag is decided here and nowhere else.
        return TotalsByKey(await LoadVouchersAsync(counted, canViewCost, ct), key);
    }

    /// Customers (the voucher's partner) or employees (the voucher's owner), grouped in memory by `key`.
    private static Dictionary<Guid, Totals> TotalsByKey(List<VoucherRow> vouchers, Func<VoucherRow, Guid?> key) =>
        // XBH and NTL require a customer, so no counted voucher lacks a partner; the filter only drops a null key.
        vouchers.Where(v => key(v) is not null)
            .GroupBy(v => key(v)!.Value)
            .ToDictionary(g => g.Key, g => new Totals(g.Sum(v => v.Revenue), g.Sum(v => v.Cost)));

    /// Products: revenue from the voucher lines and cost from the ledger rows, each grouped by product in the database.
    private async Task<Dictionary<Guid, Totals>> TotalsByProductAsync(Counted counted, bool withCost, CancellationToken ct)
    {
        var vouchers = counted.Vouchers;
        var returnIds = counted.ReturnIds;

        var revenues = await _db.StockVoucherLines.AsNoTracking()
            .Where(l => vouchers.Any(v => v.Id == l.StockVoucherId))
            .GroupBy(l => l.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Revenue = g.Sum(l => (returnIds.Contains(l.StockVoucher!.ReasonId) ? -1m : 1m)
                    * (l.Amount - l.DiscountAmount - l.OrderDiscountAllocated
                        - (l.StockVoucher.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m))),
            })
            .ToListAsync(ct);

        // Sale rows carry CostAmount and no InValue, return rows the opposite, so one difference gives the net cost.
        var costs = withCost
            ? await _db.InventoryLedger.AsNoTracking()
                .Where(e => (e.SourceType == LedgerSourceType.StockOut || e.SourceType == LedgerSourceType.StockIn)
                    && vouchers.Any(v => v.Id == e.SourceId))
                .GroupBy(e => e.ProductId)
                .Select(g => new { ProductId = g.Key, Cost = g.Sum(e => e.CostAmount ?? 0m) - g.Sum(e => e.InValue) })
                .ToDictionaryAsync(c => c.ProductId, c => c.Cost, ct)
            : new Dictionary<Guid, decimal>();

        return revenues.ToDictionary(r => r.ProductId, r => new Totals(r.Revenue, costs.GetValueOrDefault(r.ProductId)));
    }

    /// Code and name of each customer, product or employee, from the lookups every metrics service shares.
    private async Task<Dictionary<Guid, (string? Code, string Name)>> LoadNamesAsync(
        SalesTopBy by, List<Guid> ids, CancellationToken ct)
    {
        switch (by)
        {
            case SalesTopBy.Customer:
                return (await _lookups.PartnersAsync(ids, ct))
                    .ToDictionary(c => c.Key, c => ((string?)c.Value.Code, c.Value.Name));
            case SalesTopBy.Product:
                return (await _lookups.ProductsAsync(ids, ct))
                    .ToDictionary(p => p.Key, p => ((string?)p.Value.Code, p.Value.Name));
            case SalesTopBy.Employee:
                return (await _lookups.UserNamesAsync(ids, ct))
                    .ToDictionary(u => u.Key, u => ((string?)null, u.Value));
            default:
                throw new ArgumentOutOfRangeException(nameof(by), by, "Unknown top list.");
        }
    }

    /// The query of counted vouchers and the ids of the NTL reason (the vouchers that count negative).
    private sealed record Counted(IQueryable<StockVoucher> Vouchers, List<Guid> ReturnIds);

    /// Revenue and Cost are signed: a return counts negative.
    private sealed record VoucherRow(Guid VoucherId, DateTimeOffset At, Guid? PartnerId, Guid OwnerUserId,
        decimal Revenue, decimal Cost);

    private sealed record Totals(decimal Revenue, decimal Cost);
}
