using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Options;
using OrderMgmt.Application.Dashboards.Common;
using OrderMgmt.Application.Dashboards.Executive.Common;
using OrderMgmt.Application.Dashboards.Executive.Services;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Reports.Common;
using OrderMgmt.Application.Sales.Quotations.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Sales.Quotations.Services;

public class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IOptionsMonitor<FeatureOptions> _features;
    private readonly SalesRevenueSource _source;
    private readonly ICurrentBranch _currentBranch;
    private readonly ExecutiveLookups _lookups;

    public DashboardService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IOptionsMonitor<FeatureOptions> features,
        SalesRevenueSource source,
        ICurrentBranch currentBranch,
        ExecutiveLookups lookups)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _features = features;
        _source = source;
        _currentBranch = currentBranch;
        _lookups = lookups;
    }

    private IQueryable<Quotation> ApplyScope(Guid? requestedSaleUserId)
    {
        var q = _db.Quotations.AsNoTracking().Where(x => !x.IsDeleted);
        if (!_features.CurrentValue.QuotationOwnerScope)
            return requestedSaleUserId.HasValue
                ? q.Where(x => x.OwnerUserId == requestedSaleUserId.Value)
                : q;
        if (_currentUser.HasPermission(Permissions.Quotations.ViewAll))
            return requestedSaleUserId.HasValue
                ? q.Where(x => x.OwnerUserId == requestedSaleUserId.Value)
                : q;
        var uid = _currentUser.UserId ?? Guid.Empty;
        return q.Where(x => x.OwnerUserId == uid);
    }

    private static decimal? DeltaOf(decimal cur, decimal prev)
        => prev == 0m ? null : Math.Round((cur - prev) / prev * 100m, 2, MidpointRounding.AwayFromZero);

    private static async Task<List<(DateOnly Date, decimal Total, int Count)>> GroupByRevenueDateAsync(
        IQueryable<Quotation> q, string dateMode, CancellationToken ct)
    {
        if (dateMode == RevenueDateField.AccountingConfirmedAt)
            return (await q
                .GroupBy(x => DateOnly.FromDateTime(x.AccountingConfirmedAt!.Value))
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() })
                .ToListAsync(ct))
                .Select(r => (r.Date, r.Total, r.Count)).ToList();

        if (dateMode == RevenueDateField.ConfirmedAt)
            return (await q
                .GroupBy(x => DateOnly.FromDateTime(x.ConfirmedAt!.Value))
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() })
                .ToListAsync(ct))
                .Select(r => (r.Date, r.Total, r.Count)).ToList();

        return (await q
            .GroupBy(x => x.QuotationDate)
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() })
            .ToListAsync(ct))
            .Select(r => (r.Date, r.Total, r.Count)).ToList();
    }

    /// The working branch, and the owner the revenue is limited to: the requested one, or the current user when the
    /// owner scope is on and the user cannot view all quotations (the same rule as ApplyScope).
    private async Task<(Guid BranchId, Guid? OwnerUserId)> RevenueScopeAsync(Guid? requestedSaleUserId, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var viewsAll = !_features.CurrentValue.QuotationOwnerScope
            || _currentUser.HasPermission(Permissions.Quotations.ViewAll);
        return (branchId, viewsAll ? requestedSaleUserId : _currentUser.UserId ?? Guid.Empty);
    }

    /// The counted stock-outs of the working branch on the Vietnam days [from, to].
    private async Task<CountedSales> CountedAsync(DateOnly from, DateOnly to, Guid? saleUserId, CancellationToken ct)
    {
        var (branchId, ownerUserId) = await RevenueScopeAsync(saleUserId, ct);
        return await _source.BuildAsync([branchId], ownerUserId,
            VnTime.StartOfDay(from), VnTime.StartOfNextDay(to), ct);
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, Guid? saleUserId, CancellationToken ct)
    {
        var today = VnTime.ToVnDate(_clock.UtcNow);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo = to ?? today;
        if (rangeTo < rangeFrom) rangeTo = rangeFrom;
        var period = ExecutivePeriod.Create(rangeFrom, rangeTo, today);
        var prevFrom = period.PrevFrom;
        var prevTo = period.PrevTo;

        var curFromDt = rangeFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var curToDt = rangeTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevFromDt = prevFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevToDt = prevTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var scope = ApplyScope(saleUserId);

        // Revenue — the current and the previous period in one read, told apart by their instant
        var vouchers = await (await CountedAsync(prevFrom, rangeTo, saleUserId, ct)).VoucherRevenues().ToListAsync(ct);
        var curRevenue = vouchers.Where(v => v.VoucherAt >= period.Start).Sum(v => v.Revenue);
        var prevRevenue = vouchers.Where(v => v.VoucherAt < period.Start).Sum(v => v.Revenue);

        // Total quotation count — always by QuotationDate (pipeline metric); cur+prev in one query
        var totalCountRow = await scope
            .Where(q => q.QuotationDate >= prevFrom && q.QuotationDate <= rangeTo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cur = g.Count(x => x.QuotationDate >= rangeFrom && x.QuotationDate <= rangeTo),
                Prev = g.Count(x => x.QuotationDate >= prevFrom && x.QuotationDate <= prevTo),
            })
            .FirstOrDefaultAsync(ct);
        var curTotalCount = totalCountRow?.Cur ?? 0;
        var prevTotalCount = totalCountRow?.Prev ?? 0;

        // Cancelled count — always by CancelledAt; cur+prev in one query
        var cancelledRow = await scope
            .Where(q => q.Status == QuotationStatus.Cancelled && q.CancelledAt != null
                && q.CancelledAt >= prevFromDt && q.CancelledAt < curToDt)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cur = g.Count(x => x.CancelledAt >= curFromDt && x.CancelledAt < curToDt),
                Prev = g.Count(x => x.CancelledAt >= prevFromDt && x.CancelledAt < prevToDt),
            })
            .FirstOrDefaultAsync(ct);
        var curCancelled = cancelledRow?.Cur ?? 0;
        var prevCancelled = cancelledRow?.Prev ?? 0;

        // Revenue spark and today — the last 7 Vietnam days, and today's figure from the same read
        var sparkFrom = today.AddDays(-6);
        var sparkVouchers = await (await CountedAsync(sparkFrom, today, saleUserId, ct)).VoucherRevenues().ToListAsync(ct);
        var revenueByDay = sparkVouchers
            .GroupBy(v => VnTime.ToVnDate(v.VoucherAt))
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Revenue));
        var sparkRevenue = new decimal[7];
        for (var i = 0; i < 7; i++)
            sparkRevenue[i] = revenueByDay.GetValueOrDefault(sparkFrom.AddDays(i));
        var todayRevenue = revenueByDay.GetValueOrDefault(today);

        // Count spark — quotations by QuotationDate over the 7 days ending today, like TotalCount
        var quotationToday = DateOnly.FromDateTime(_clock.Now.DateTime);
        var countFrom = quotationToday.AddDays(-6);
        var countByDay = await scope
            .Where(q => q.QuotationDate >= countFrom && q.QuotationDate <= quotationToday)
            .GroupBy(q => q.QuotationDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(r => r.Date, r => r.Count, ct);
        var sparkCount = new decimal[7];
        for (var i = 0; i < 7; i++)
            sparkCount[i] = countByDay.GetValueOrDefault(countFrom.AddDays(i));

        // Funnel — always by QuotationDate (pipeline, not revenue)
        var funnelRows = await scope
            .Where(q => q.QuotationDate >= rangeFrom && q.QuotationDate <= rangeTo)
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int draft = 0, sent = 0, confirmed = 0, cancelled = 0;
        foreach (var r in funnelRows)
        {
            switch (r.Status)
            {
                case QuotationStatus.Draft: draft = r.Count; break;
                case QuotationStatus.Sent: sent = r.Count; break;
                case QuotationStatus.Confirmed: confirmed = r.Count; break;
                case QuotationStatus.Cancelled: cancelled = r.Count; break;
            }
        }
        decimal? sentRate = draft == 0 ? null : Math.Round((decimal)(sent + confirmed) / draft * 100m, 2, MidpointRounding.AwayFromZero);
        decimal? confirmRate = sent == 0 ? null : Math.Round((decimal)confirmed / sent * 100m, 2, MidpointRounding.AwayFromZero);

        return new DashboardSummaryDto
        {
            From = rangeFrom,
            To = rangeTo,
            PrevFrom = prevFrom,
            PrevTo = prevTo,
            TodayRevenue = new KpiDto { Value = todayRevenue, DeltaPct = null, Spark = Array.Empty<decimal>() },
            RangeRevenue = new KpiDto { Value = curRevenue, DeltaPct = DeltaOf(curRevenue, prevRevenue), Spark = sparkRevenue },
            TotalCount = new KpiDto { Value = curTotalCount, DeltaPct = DeltaOf(curTotalCount, prevTotalCount), Spark = sparkCount },
            CancelledCount = new KpiDto { Value = curCancelled, DeltaPct = DeltaOf(curCancelled, prevCancelled), Spark = Array.Empty<decimal>() },
            Funnel = new FunnelDto
            {
                Draft = draft,
                Sent = sent,
                Confirmed = confirmed,
                Cancelled = cancelled,
                SentRate = sentRate,
                ConfirmRate = confirmRate,
            },
        };
    }

    public async Task<RevenueSeriesDto> GetRevenueSeriesAsync(DateOnly from, DateOnly to, string granularity, Guid? saleUserId, CancellationToken ct)
    {
        if (to < from) to = from;

        var vouchers = await (await CountedAsync(from, to, saleUserId, ct)).VoucherRevenues().ToListAsync(ct);
        // A return lowers the day's total but is not a sale, so it is not counted.
        var byDay = vouchers
            .GroupBy(v => VnTime.ToVnDate(v.VoucherAt))
            .ToDictionary(g => g.Key, g => (Total: g.Sum(v => v.Revenue), Count: g.Count(v => !v.IsReturn)));
        var points = new List<RevenuePointDto>();

        if (string.Equals(granularity, "month", StringComparison.OrdinalIgnoreCase))
        {
            var months = new SortedDictionary<DateOnly, (decimal Total, int Count)>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var key = new DateOnly(d.Year, d.Month, 1);
                byDay.TryGetValue(d, out var v);
                months.TryGetValue(key, out var cur);
                months[key] = (cur.Total + v.Total, cur.Count + v.Count);
            }
            foreach (var kv in months)
                points.Add(new RevenuePointDto { Date = kv.Key, Total = kv.Value.Total, VoucherCount = kv.Value.Count });
        }
        else if (string.Equals(granularity, "week", StringComparison.OrdinalIgnoreCase))
        {
            var weeks = new SortedDictionary<DateOnly, (decimal Total, int Count)>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var offset = ((int)d.DayOfWeek + 6) % 7;
                var key = d.AddDays(-offset);
                byDay.TryGetValue(d, out var v);
                weeks.TryGetValue(key, out var cur);
                weeks[key] = (cur.Total + v.Total, cur.Count + v.Count);
            }
            foreach (var kv in weeks)
                points.Add(new RevenuePointDto { Date = kv.Key, Total = kv.Value.Total, VoucherCount = kv.Value.Count });
        }
        else
        {
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                byDay.TryGetValue(d, out var v);
                points.Add(new RevenuePointDto { Date = d, Total = v.Total, VoucherCount = v.Count });
            }
        }

        return new RevenueSeriesDto { Points = points };
    }

    public async Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(DateOnly from, DateOnly to, int limit, Guid? saleUserId, CancellationToken ct)
    {
        if (limit <= 0) limit = 5;
        if (to < from) to = from;

        var counted = await CountedAsync(from, to, saleUserId, ct);
        var rows = await counted.VoucherRevenues()
            .Where(v => v.PartnerId != null)
            .GroupBy(v => v.PartnerId!.Value)
            // A return lowers the revenue but is not a sale, so it is not counted.
            .Select(g => new { PartnerId = g.Key, Revenue = g.Sum(v => v.Revenue), Count = g.Count(v => !v.IsReturn) })
            .ToListAsync(ct);
        var names = await _lookups.PartnersAsync(rows.Select(r => r.PartnerId).ToList(), ct);

        return rows
            .Select(r => new TopCustomerDto
            {
                CustomerId = r.PartnerId,
                CustomerName = names.TryGetValue(r.PartnerId, out var partner) ? partner.Name : string.Empty,
                Revenue = r.Revenue,
                VoucherCount = r.Count,
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.CustomerName, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateOnly from, DateOnly to, int limit, Guid? saleUserId, CancellationToken ct)
    {
        if (limit <= 0) limit = 5;
        if (to < from) to = from;

        var counted = await CountedAsync(from, to, saleUserId, ct);
        // Quantity is signed: a return takes its goods off the total.
        var rows = await counted.LineRevenues(_db.StockVoucherLines)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Revenue = g.Sum(l => l.Revenue), Quantity = g.Sum(l => l.Quantity) })
            .OrderByDescending(r => r.Revenue)
            .ThenBy(r => r.ProductId)
            .Take(limit)
            .ToListAsync(ct);
        var names = await _lookups.ProductsAsync(rows.Select(r => r.ProductId).ToList(), ct);

        return rows
            .Select(r => new TopProductDto
            {
                ProductId = r.ProductId,
                ProductName = names.TryGetValue(r.ProductId, out var product) ? product.Name : string.Empty,
                Revenue = r.Revenue,
                Quantity = r.Quantity,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ActivityItemDto>> GetRecentActivityAsync(int limit, CancellationToken ct)
    {
        if (limit <= 0) limit = 10;
        var fetchPerSource = Math.Max(limit, 30);
        var scope = ApplyScope(null);

        var createdRows = await scope
            .OrderByDescending(q => q.CreatedAt)
            .Take(fetchPerSource)
            .Select(q => new
            {
                q.Id,
                q.Code,
                q.CustomerName,
                At = q.CreatedAt,
                ActorId = q.CreatedBy,
                q.Total,
            })
            .ToListAsync(ct);

        var confirmedRows = await scope
            .Where(q => q.ConfirmedAt != null)
            .OrderByDescending(q => q.ConfirmedAt)
            .Take(fetchPerSource)
            .Select(q => new
            {
                q.Id,
                q.Code,
                q.CustomerName,
                At = q.ConfirmedAt!.Value,
                ActorId = q.ConfirmedByUserId,
                q.Total,
            })
            .ToListAsync(ct);

        var cancelledRows = await scope
            .Where(q => q.CancelledAt != null)
            .OrderByDescending(q => q.CancelledAt)
            .Take(fetchPerSource)
            .Select(q => new
            {
                q.Id,
                q.Code,
                q.CustomerName,
                At = q.CancelledAt!.Value,
                q.Total,
            })
            .ToListAsync(ct);

        var actorIds = createdRows.Select(r => r.ActorId)
            .Concat(confirmedRows.Select(r => r.ActorId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var nameMap = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        string? LookupName(Guid? id) =>
            id.HasValue && nameMap.TryGetValue(id.Value, out var n) ? n : null;

        var created = createdRows.Select(r => new ActivityItemDto
        {
            At = r.At.UtcDateTime,
            Type = "created",
            QuotationId = r.Id,
            Code = r.Code,
            CustomerName = r.CustomerName,
            ActorName = LookupName(r.ActorId),
            Amount = r.Total,
        });

        var confirmed = confirmedRows.Select(r => new ActivityItemDto
        {
            At = r.At,
            Type = "confirmed",
            QuotationId = r.Id,
            Code = r.Code,
            CustomerName = r.CustomerName,
            ActorName = LookupName(r.ActorId),
            Amount = r.Total,
        });

        var cancelled = cancelledRows.Select(r => new ActivityItemDto
        {
            At = r.At,
            Type = "cancelled",
            QuotationId = r.Id,
            Code = r.Code,
            CustomerName = r.CustomerName,
            ActorName = null,
            Amount = r.Total,
        });

        return created.Concat(confirmed).Concat(cancelled)
            .OrderByDescending(a => a.At)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesLeaderboardItemDto>> GetSalesLeaderboardAsync(DateOnly from, DateOnly to, int limit, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(Permissions.Quotations.ViewAll))
            throw new ForbiddenException("Bạn không có quyền xem bảng xếp hạng sale.");

        if (limit <= 0) limit = 10;
        if (to < from) to = from;
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);

        var rangeLen = to.DayNumber - from.DayNumber + 1;
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(rangeLen - 1));

        var scope = _db.Quotations.AsNoTracking().Where(q => !q.IsDeleted);

        var curRows = await RevenueFilterHelper.ApplyRevenueFilter(scope, dateMode, from, to)
            .GroupBy(q => q.OwnerUserId)
            .Select(g => new { UserId = g.Key, Revenue = g.Sum(x => x.Total), Count = g.Count() })
            .ToListAsync(ct);

        var prevRows = await RevenueFilterHelper.ApplyRevenueFilter(scope, dateMode, prevFrom, prevTo)
            .GroupBy(q => q.OwnerUserId)
            .Select(g => new { UserId = g.Key, Revenue = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        var totalCountRows = await scope
            .Where(q => q.QuotationDate >= from && q.QuotationDate <= to)
            .GroupBy(q => q.OwnerUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var prevRevenueMap = prevRows.ToDictionary(r => r.UserId, r => r.Revenue);
        var totalCountMap = totalCountRows.ToDictionary(r => r.UserId, r => r.Count);

        var topUserIds = curRows
            .OrderByDescending(r => r.Revenue)
            .Take(limit)
            .Select(r => r.UserId)
            .ToList();

        var names = await _db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => topUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);
        var nameMap = names.ToDictionary(n => n.Id, n => n.FullName);

        return curRows
            .OrderByDescending(r => r.Revenue)
            .Take(limit)
            .Select(r =>
            {
                prevRevenueMap.TryGetValue(r.UserId, out var prevRevenue);
                totalCountMap.TryGetValue(r.UserId, out var totalCount);
                return new SalesLeaderboardItemDto
                {
                    UserId = r.UserId,
                    FullName = nameMap.GetValueOrDefault(r.UserId) ?? "—",
                    Revenue = r.Revenue,
                    ConfirmedCount = r.Count,
                    ConversionRate = totalCount == 0
                        ? null
                        : Math.Round((decimal)r.Count / totalCount * 100m, 2, MidpointRounding.AwayFromZero),
                    DeltaPct = DeltaOf(r.Revenue, prevRevenue),
                };
            })
            .ToList();
    }
}
