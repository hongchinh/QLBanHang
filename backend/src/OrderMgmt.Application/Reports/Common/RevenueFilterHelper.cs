using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Reports.Common;

public static class RevenueFilterHelper
{
    public static async Task<string> GetDateModeAsync(IAppDbContext db, CancellationToken ct)
    {
        var mode = await db.QuotationSystemSettings
            .AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => s.RevenueReportingDateField)
            .FirstOrDefaultAsync(ct);
        return mode ?? RevenueDateField.QuotationDate;
    }

    public static IQueryable<Quotation> ApplyRevenueDateRangeFilter(
        IQueryable<Quotation> q, string dateMode, DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue && !to.HasValue) return q;

        switch (dateMode)
        {
            case RevenueDateField.ConfirmedAt:
            {
                var fromDt = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var toDt   = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return q.Where(x =>
                    x.ConfirmedAt != null && x.CancelledAt == null
                    && (!fromDt.HasValue || x.ConfirmedAt >= fromDt.Value)
                    && (!toDt.HasValue   || x.ConfirmedAt < toDt.Value));
            }
            case RevenueDateField.AccountingConfirmedAt:
            {
                var fromDt = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var toDt   = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return q.Where(x =>
                    x.AccountingConfirmedAt != null && x.CancelledAt == null
                    && (!fromDt.HasValue || x.AccountingConfirmedAt >= fromDt.Value)
                    && (!toDt.HasValue   || x.AccountingConfirmedAt < toDt.Value));
            }
            default:
                return q.Where(x =>
                    x.CancelledAt == null
                    && (!from.HasValue || x.QuotationDate >= from.Value)
                    && (!to.HasValue   || x.QuotationDate <= to.Value));
        }
    }

    public static IQueryable<Quotation> ApplyRevenueFilter(
        IQueryable<Quotation> q, string dateMode, DateOnly from, DateOnly to)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDt = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return dateMode switch
        {
            RevenueDateField.AccountingConfirmedAt => q.Where(x =>
                x.Status == QuotationStatus.AccountingConfirmed
                && x.CancelledAt == null
                && x.AccountingConfirmedAt >= fromDt
                && x.AccountingConfirmedAt < toDt),
            RevenueDateField.ConfirmedAt => q.Where(x =>
                (x.Status == QuotationStatus.Confirmed || x.Status == QuotationStatus.AccountingConfirmed)
                && x.CancelledAt == null
                && x.ConfirmedAt >= fromDt
                && x.ConfirmedAt < toDt),
            _ => q.Where(x =>
                (x.Status == QuotationStatus.Confirmed || x.Status == QuotationStatus.AccountingConfirmed)
                && x.CancelledAt == null
                && x.QuotationDate >= from
                && x.QuotationDate <= to),
        };
    }
}
