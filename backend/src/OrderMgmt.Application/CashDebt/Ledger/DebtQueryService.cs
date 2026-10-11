using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

public class DebtQueryService : IDebtQueryService
{
    private readonly IAppDbContext _db;

    public DebtQueryService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> BalanceAsync(Guid branchId, Guid partnerId, DebtSide side, DateTimeOffset? at = null,
        CancellationToken ct = default)
    {
        var query = _db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.PartnerId == partnerId && e.Side == side);
        if (at is { } instant)
        {
            var utc = VnTime.ToUtc(instant);
            query = query.Where(e => e.PostedAt <= utc);
        }

        return await query.SumAsync(e => e.Direction == DebtDirection.Increase ? e.Amount : -e.Amount, ct);
    }

    public async Task<IReadOnlyList<OpenItemDto>> OpenItemsAsync(OpenItemsQuery query, CancellationToken ct = default)
    {
        // A settlement pairs two entries of one branch, partner and side, so both its ends are among these entries.
        var entries = await _db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == query.BranchId && e.PartnerId == query.PartnerId && e.Side == query.Side)
            .ToListAsync(ct);
        var entryIds = entries.Select(e => e.Id).ToList();
        var settlements = await _db.DebtSettlements.AsNoTracking()
            .Where(s => entryIds.Contains(s.IncreaseEntryId))
            .Select(s => new { s.IncreaseEntryId, s.DecreaseEntryId, s.Amount, s.Origin, s.BatchId })
            .ToListAsync(ct);

        // P26: the source's own entries are left out, and the settlements an edit of the source replaces (its own
        // AtVoucher batch) no longer count as settled. Every other settlement on its entries stays on save.
        var forSourceId = query.ForSourceId;
        var sourceEntryIds = query.ForSourceType is { } sourceType && forSourceId is { } sourceId
            ? entries.Where(e => e.SourceType == sourceType && e.SourceId == sourceId).Select(e => e.Id).ToHashSet()
            : new HashSet<Guid>();

        var settled = entryIds.ToDictionary(id => id, _ => 0m);
        foreach (var s in settlements)
        {
            if ((sourceEntryIds.Contains(s.IncreaseEntryId) || sourceEntryIds.Contains(s.DecreaseEntryId))
                && s.Origin == SettlementOrigin.AtVoucher && s.BatchId == forSourceId)
                continue;
            settled[s.IncreaseEntryId] += s.Amount;
            settled[s.DecreaseEntryId] += s.Amount;
        }

        return entries
            .Where(e => !sourceEntryIds.Contains(e.Id))
            .Where(e => query.Direction is null || e.Direction == query.Direction)
            .Where(e => e.Amount - settled[e.Id] > 0)
            .OrderBy(e => e.DueDate ?? e.DocDate)
            .ThenBy(e => e.DocDate)
            .ThenBy(e => e.DocCode, StringComparer.Ordinal)
            .Select(e => new OpenItemDto
            {
                EntryId = e.Id,
                SourceType = e.SourceType,
                SourceId = e.SourceId,
                DocCode = e.DocCode,
                DocDate = e.DocDate,
                DueDate = e.DueDate,
                PostedAt = e.PostedAt,
                Direction = e.Direction,
                Amount = e.Amount,
                Settled = settled[e.Id],
                Open = e.Amount - settled[e.Id],
                Description = e.Description,
                OwnerUserId = e.OwnerUserId,
            })
            .ToList();
    }
}
