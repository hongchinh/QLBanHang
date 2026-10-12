using Microsoft.EntityFrameworkCore;
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
                        DaysOutstanding = Math.Max(0, request.At.DayNumber - i.DocDate.DayNumber),
                        DaysOverdue = due < request.At ? request.At.DayNumber - due.DayNumber : 0,
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
}
