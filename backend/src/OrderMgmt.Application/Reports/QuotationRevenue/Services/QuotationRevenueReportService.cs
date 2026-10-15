using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Common.Options;
using OrderMgmt.Application.Reports.Common;
using OrderMgmt.Application.Reports.QuotationRevenue.Interfaces;
using OrderMgmt.Application.Reports.QuotationRevenue.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Reports.QuotationRevenue.Services;

/// The quotation revenue the overview dashboard used to show, moved here unchanged for the Doanh thu report (D10).
public class QuotationRevenueReportService : IQuotationRevenueReportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IOptionsMonitor<FeatureOptions> _features;

    public QuotationRevenueReportService(
        IAppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IOptionsMonitor<FeatureOptions> features)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _features = features;
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

    private static async Task<(decimal Cur, decimal Prev)> GetPeriodRevenuesAsync(
        IQueryable<Quotation> scope, string dateMode,
        DateOnly curFrom, DateOnly curTo, DateOnly prevFrom, DateOnly prevTo,
        CancellationToken ct)
    {
        var curFromDt = curFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var curToDt = curTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevFromDt = prevFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevToDt = prevTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        if (dateMode == RevenueDateField.AccountingConfirmedAt)
        {
            var row = await scope
                .Where(q => q.Status == QuotationStatus.AccountingConfirmed
                    && q.CancelledAt == null
                    && q.AccountingConfirmedAt >= prevFromDt
                    && q.AccountingConfirmedAt < curToDt)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Cur = g.Sum(x => x.AccountingConfirmedAt >= curFromDt && x.AccountingConfirmedAt < curToDt ? x.Total : 0m),
                    Prev = g.Sum(x => x.AccountingConfirmedAt >= prevFromDt && x.AccountingConfirmedAt < prevToDt ? x.Total : 0m),
                })
                .FirstOrDefaultAsync(ct);
            return (row?.Cur ?? 0m, row?.Prev ?? 0m);
        }

        if (dateMode == RevenueDateField.ConfirmedAt)
        {
            var row = await scope
                .Where(q => (q.Status == QuotationStatus.Confirmed || q.Status == QuotationStatus.AccountingConfirmed)
                    && q.CancelledAt == null
                    && q.ConfirmedAt >= prevFromDt
                    && q.ConfirmedAt < curToDt)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Cur = g.Sum(x => x.ConfirmedAt >= curFromDt && x.ConfirmedAt < curToDt ? x.Total : 0m),
                    Prev = g.Sum(x => x.ConfirmedAt >= prevFromDt && x.ConfirmedAt < prevToDt ? x.Total : 0m),
                })
                .FirstOrDefaultAsync(ct);
            return (row?.Cur ?? 0m, row?.Prev ?? 0m);
        }

        // QuotationDate
        {
            var row = await scope
                .Where(q => (q.Status == QuotationStatus.Confirmed || q.Status == QuotationStatus.AccountingConfirmed)
                    && q.CancelledAt == null
                    && q.QuotationDate >= prevFrom
                    && q.QuotationDate <= curTo)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Cur = g.Sum(x => x.QuotationDate >= curFrom && x.QuotationDate <= curTo ? x.Total : 0m),
                    Prev = g.Sum(x => x.QuotationDate >= prevFrom && x.QuotationDate <= prevTo ? x.Total : 0m),
                })
                .FirstOrDefaultAsync(ct);
            return (row?.Cur ?? 0m, row?.Prev ?? 0m);
        }
    }

    public async Task<QuotationRevenueSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, Guid? saleUserId, CancellationToken ct)
    {
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);

        var today = DateOnly.FromDateTime(_clock.Now.DateTime);
        var rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var rangeTo = to ?? today;
        if (rangeTo < rangeFrom) rangeTo = rangeFrom;
        var rangeLen = rangeTo.DayNumber - rangeFrom.DayNumber + 1;
        var prevTo = rangeFrom.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(rangeLen - 1));

        var curFromDt = rangeFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var curToDt = rangeTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevFromDt = prevFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevToDt = prevTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var scope = ApplyScope(saleUserId);

        // Revenue metrics — cur+prev in one query
        var (curRevenue, prevRevenue) = await GetPeriodRevenuesAsync(scope, dateMode, rangeFrom, rangeTo, prevFrom, prevTo, ct);

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

        // Spark — last 7 days, mode-aware date grouping
        var sparkFrom = today.AddDays(-6);
        var sparkRows = await GroupByRevenueDateAsync(
            RevenueFilterHelper.ApplyRevenueFilter(scope, dateMode, sparkFrom, today), dateMode, ct);
        var sparkByDay = sparkRows.ToDictionary(r => r.Date, r => (r.Total, r.Count));
        var sparkRevenue = new decimal[7];
        var sparkCount = new decimal[7];
        for (var i = 0; i < 7; i++)
        {
            var d = today.AddDays(-6 + i);
            if (sparkByDay.TryGetValue(d, out var v))
            {
                sparkRevenue[i] = v.Total;
                sparkCount[i] = v.Count;
            }
        }

        return new QuotationRevenueSummaryDto
        {
            From = rangeFrom,
            To = rangeTo,
            PrevFrom = prevFrom,
            PrevTo = prevTo,
            RangeRevenue = new KpiDto { Value = curRevenue, DeltaPct = DeltaOf(curRevenue, prevRevenue), Spark = sparkRevenue },
            TotalCount = new KpiDto { Value = curTotalCount, DeltaPct = DeltaOf(curTotalCount, prevTotalCount), Spark = sparkCount },
            CancelledCount = new KpiDto { Value = curCancelled, DeltaPct = DeltaOf(curCancelled, prevCancelled), Spark = Array.Empty<decimal>() },
        };
    }

    public async Task<QuotationRevenueSeriesDto> GetSeriesAsync(DateOnly from, DateOnly to, string granularity, Guid? saleUserId, CancellationToken ct)
    {
        if (to < from) to = from;
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);

        var dayRows = await GroupByRevenueDateAsync(
            RevenueFilterHelper.ApplyRevenueFilter(ApplyScope(saleUserId), dateMode, from, to), dateMode, ct);

        var byDay = dayRows.ToDictionary(r => r.Date, r => (r.Total, r.Count));
        var points = new List<QuotationRevenuePointDto>();

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
                points.Add(new QuotationRevenuePointDto { Date = kv.Key, Total = kv.Value.Total, ConfirmedCount = kv.Value.Count });
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
                points.Add(new QuotationRevenuePointDto { Date = kv.Key, Total = kv.Value.Total, ConfirmedCount = kv.Value.Count });
        }
        else
        {
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                byDay.TryGetValue(d, out var v);
                points.Add(new QuotationRevenuePointDto { Date = d, Total = v.Total, ConfirmedCount = v.Count });
            }
        }

        return new QuotationRevenueSeriesDto { Points = points };
    }

    public async Task<IReadOnlyList<QuotationTopCustomerDto>> GetTopCustomersAsync(DateOnly from, DateOnly to, int limit, Guid? saleUserId, CancellationToken ct)
    {
        if (limit <= 0) limit = 5;
        if (to < from) to = from;
        var dateMode = await RevenueFilterHelper.GetDateModeAsync(_db, ct);

        return await RevenueFilterHelper.ApplyRevenueFilter(ApplyScope(saleUserId), dateMode, from, to)
            .GroupBy(q => new { q.CustomerId, q.CustomerName })
            .Select(g => new QuotationTopCustomerDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.CustomerName,
                Revenue = g.Sum(x => x.Total),
                QuotationCount = g.Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .Take(limit)
            .ToListAsync(ct);
    }
}
