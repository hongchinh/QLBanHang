using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.CashDebt.Reports.Interfaces;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Services;

/// Debt position reports of the working branch, read from the debt ledger and its settlements.
public class DebtPositionReportService : IDebtPositionReportService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentBranch _currentBranch;
    private readonly ITransactionRunner _transaction;

    public DebtPositionReportService(IAppDbContext db, ICurrentBranch currentBranch, ITransactionRunner transaction)
    {
        _db = db;
        _currentBranch = currentBranch;
        _transaction = transaction;
    }

    /// One REPEATABLE READ snapshot, so entries and settlements see the same ledger.
    public Task<DebtDocumentsReportDto> GetDocumentsAsync(DebtDocumentsReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildDocumentsAsync(request, c), ct);

    private async Task<DebtDocumentsReportDto> BuildDocumentsAsync(DebtDocumentsReportRequest request, CancellationToken ct)
    {
        var branchId = await _currentBranch.GetIdAsync(ct);
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.At));
        var open = (await DebtPositionQuery.LoadAsync(_db, branchId, request.Side, request.PartnerId, end, ct))
            .Where(i => i.Open > 0m)
            .ToList();

        // Soft-deleted partners keep their ledger rows, so they stay in the report.
        var partnerIds = open.Select(i => i.PartnerId).Distinct().ToList();
        var info = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => partnerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);

        var groups = new List<DebtDocumentsGroupDto>();
        foreach (var byPartner in open.GroupBy(i => i.PartnerId))
        {
            var items = byPartner.Where(i => i.Direction == DebtDirection.Increase)
                .OrderBy(i => i.DueDate ?? i.DocDate).ThenBy(i => i.DocDate)
                .ThenBy(i => i.DocCode, StringComparer.Ordinal).ThenBy(i => i.EntryId)
                .Select(i =>
                {
                    var due = i.DueDate ?? i.DocDate;
                    return new DebtDocumentRowDto
                    {
                        EntryId = i.EntryId,
                        SourceType = i.SourceType,
                        SourceId = i.SourceId,
                        DocCode = i.DocCode,
                        DocDate = i.DocDate,
                        DueDate = due,
                        Description = i.Description,
                        Amount = i.Amount,
                        Settled = i.Settled,
                        Remaining = i.Open,
                        DaysOutstanding = DebtDates.DaysOutstanding(request.At, i.DocDate),
                        DaysOverdue = DebtDates.DaysOverdue(request.At, due),
                    };
                }).ToList();
            var unallocated = byPartner.Where(i => i.Direction == DebtDirection.Decrease)
                .OrderBy(i => i.DocDate).ThenBy(i => i.DocCode, StringComparer.Ordinal).ThenBy(i => i.EntryId)
                .Select(i => new DebtUnallocatedRowDto
                {
                    EntryId = i.EntryId,
                    SourceType = i.SourceType,
                    SourceId = i.SourceId,
                    DocCode = i.DocCode,
                    DocDate = i.DocDate,
                    Description = i.Description,
                    Amount = i.Amount,
                    Settled = i.Settled,
                    Remaining = i.Open,
                }).ToList();

            var partner = info.GetValueOrDefault(byPartner.Key);
            var totalRemaining = items.Sum(r => r.Remaining);
            var totalUnallocated = unallocated.Sum(r => r.Remaining);
            groups.Add(new DebtDocumentsGroupDto
            {
                PartnerId = byPartner.Key,
                PartnerCode = partner?.Code,
                PartnerName = partner?.Name,
                Items = items,
                Unallocated = unallocated,
                TotalRemaining = totalRemaining,
                TotalUnallocated = totalUnallocated,
                Balance = totalRemaining - totalUnallocated,
            });
        }
        groups = groups.OrderBy(g => g.PartnerCode, StringComparer.Ordinal).ToList();

        var remaining = groups.Sum(g => g.TotalRemaining);
        var unallocatedTotal = groups.Sum(g => g.TotalUnallocated);
        return new DebtDocumentsReportDto
        {
            Side = request.Side,
            At = request.At,
            Groups = groups,
            TotalRemaining = remaining,
            TotalUnallocated = unallocatedTotal,
            TotalBalance = remaining - unallocatedTotal,
        };
    }

    public Task<DebtOverdueReportDto> GetOverdueAsync(DebtOverdueReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildOverdueAsync(request, c), ct);

    private async Task<DebtOverdueReportDto> BuildOverdueAsync(DebtOverdueReportRequest request, CancellationToken ct)
    {
        var boundaries = DebtAging.ParseBoundaries(request.Buckets)!;
        var buckets = DebtAging.Ranges(boundaries, firstFrom: 1);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.At));
        var open = (await DebtPositionQuery.LoadAsync(_db, branchId, request.Side, request.PartnerId, end, ct))
            .Where(i => i.Open > 0m)
            .ToList();

        var overdue = open
            .Where(i => i.Direction == DebtDirection.Increase && (i.DueDate ?? i.DocDate) < request.At)
            .ToList();
        var partnerIds = overdue.Select(i => i.PartnerId).Distinct().ToList();
        var info = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => partnerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);
        var unallocatedByPartner = open
            .Where(i => i.Direction == DebtDirection.Decrease && partnerIds.Contains(i.PartnerId))
            .GroupBy(i => i.PartnerId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Open));

        var groups = new List<DebtOverdueGroupDto>();
        foreach (var byPartner in overdue.GroupBy(i => i.PartnerId))
        {
            var amounts = new decimal[buckets.Count];
            var items = byPartner
                .Select(i => new { Entry = i, Due = i.DueDate ?? i.DocDate })
                .OrderBy(x => x.Due).ThenBy(x => x.Entry.DocDate)
                .ThenBy(x => x.Entry.DocCode, StringComparer.Ordinal).ThenBy(x => x.Entry.EntryId)
                .Select(x =>
                {
                    var days = DebtDates.DaysOverdue(request.At, x.Due);
                    var bucket = DebtAging.BucketIndex(days, boundaries);
                    amounts[bucket] += x.Entry.Open;
                    return new DebtOverdueRowDto
                    {
                        EntryId = x.Entry.EntryId,
                        SourceType = x.Entry.SourceType,
                        SourceId = x.Entry.SourceId,
                        DocCode = x.Entry.DocCode,
                        DocDate = x.Entry.DocDate,
                        DueDate = x.Due,
                        Description = x.Entry.Description,
                        Amount = x.Entry.Amount,
                        Remaining = x.Entry.Open,
                        DaysOverdue = days,
                        BucketIndex = bucket,
                    };
                }).ToList();

            var partner = info.GetValueOrDefault(byPartner.Key);
            groups.Add(new DebtOverdueGroupDto
            {
                PartnerId = byPartner.Key,
                PartnerCode = partner?.Code,
                PartnerName = partner?.Name,
                Items = items,
                Amounts = amounts.ToList(),
                TotalOverdue = amounts.Sum(),
                Unallocated = unallocatedByPartner.GetValueOrDefault(byPartner.Key),
            });
        }
        groups = groups.OrderBy(g => g.PartnerCode, StringComparer.Ordinal).ToList();

        return new DebtOverdueReportDto
        {
            Side = request.Side,
            At = request.At,
            Buckets = buckets.Select(b => new AgingBucketDto { From = b.From, To = b.To }).ToList(),
            Groups = groups,
            TotalAmounts = Enumerable.Range(0, buckets.Count).Select(i => groups.Sum(g => g.Amounts[i])).ToList(),
            TotalOverdue = groups.Sum(g => g.TotalOverdue),
            TotalUnallocated = groups.Sum(g => g.Unallocated),
        };
    }

    public Task<DebtAgingReportDto> GetAgingAsync(DebtAgingReportRequest request, CancellationToken ct = default) =>
        _transaction.RunSnapshotAsync(c => BuildAgingAsync(request, c), ct);

    private async Task<DebtAgingReportDto> BuildAgingAsync(DebtAgingReportRequest request, CancellationToken ct)
    {
        var boundaries = DebtAging.ParseBoundaries(request.Buckets)!;
        var buckets = DebtAging.Ranges(boundaries, firstFrom: 0);
        var branchId = await _currentBranch.GetIdAsync(ct);
        var end = VnTime.ToUtc(VnTime.StartOfNextDay(request.At));
        var open = (await DebtPositionQuery.LoadAsync(_db, branchId, request.Side, request.PartnerId, end, ct))
            .Where(i => i.Open > 0m)
            .ToList();

        var partnerIds = open.Select(i => i.PartnerId).Distinct().ToList();
        var info = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => partnerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.Name })
            .ToDictionaryAsync(c => c.Id, ct);

        var rows = new List<DebtAgingRowDto>();
        foreach (var byPartner in open.GroupBy(i => i.PartnerId))
        {
            var amounts = new decimal[buckets.Count];
            foreach (var item in byPartner.Where(i => i.Direction == DebtDirection.Increase))
            {
                var age = DebtDates.DaysOutstanding(request.At, item.DocDate);
                amounts[DebtAging.BucketIndex(age, boundaries)] += item.Open;
            }
            var unallocated = byPartner.Where(i => i.Direction == DebtDirection.Decrease).Sum(i => i.Open);
            var partner = info.GetValueOrDefault(byPartner.Key);
            var totalOpen = amounts.Sum();
            rows.Add(new DebtAgingRowDto
            {
                PartnerId = byPartner.Key,
                PartnerCode = partner?.Code,
                PartnerName = partner?.Name,
                Amounts = amounts.ToList(),
                TotalOpen = totalOpen,
                Unallocated = unallocated,
                Balance = totalOpen - unallocated,
            });
        }
        rows = rows.OrderBy(r => r.PartnerCode, StringComparer.Ordinal).ToList();

        var totalOpenAll = rows.Sum(r => r.TotalOpen);
        var unallocatedAll = rows.Sum(r => r.Unallocated);
        return new DebtAgingReportDto
        {
            Side = request.Side,
            At = request.At,
            Buckets = buckets.Select(b => new AgingBucketDto { From = b.From, To = b.To }).ToList(),
            Rows = rows,
            Totals = new DebtAgingRowDto
            {
                Amounts = Enumerable.Range(0, buckets.Count).Select(i => rows.Sum(r => r.Amounts[i])).ToList(),
                TotalOpen = totalOpenAll,
                Unallocated = unallocatedAll,
                Balance = totalOpenAll - unallocatedAll,
            },
        };
    }
}
