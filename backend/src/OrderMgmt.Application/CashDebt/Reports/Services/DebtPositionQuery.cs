using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Reports.Services;

/// One debt entry counted at an instant, with the amount of it already matched.
internal sealed class DebtPositionItem
{
    public Guid EntryId { get; init; }
    public Guid PartnerId { get; init; }
    public DebtDirection Direction { get; init; }
    public DebtSourceType SourceType { get; init; }
    public Guid SourceId { get; init; }
    public string DocCode { get; init; } = default!;
    public DateOnly DocDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? Description { get; init; }
    public decimal Amount { get; init; }
    public decimal Settled { get; init; }
    public decimal Open => Amount - Settled;
}

internal static class DebtPositionQuery
{
    /// B3: entries of the branch and side (one partner when set) with PostedAt before endExclusive or SourceType == Opening,
    /// each with the sum of its settlements whose two entries both count. All counted entries are returned, open or not.
    public static async Task<List<DebtPositionItem>> LoadAsync(IAppDbContext db, Guid branchId, DebtSide side,
        Guid? partnerId, DateTimeOffset endExclusive, CancellationToken ct)
    {
        var counted = db.DebtEntries.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.Side == side
                && (partnerId == null || e.PartnerId == partnerId)
                && (e.PostedAt < endExclusive || e.SourceType == DebtSourceType.Opening));
        var entries = await counted.ToListAsync(ct);

        // A settlement counts only when both of its entries count; it then adds to both.
        var settlements = await db.DebtSettlements.AsNoTracking()
            .Where(s => counted.Any(e => e.Id == s.IncreaseEntryId) && counted.Any(e => e.Id == s.DecreaseEntryId))
            .Select(s => new { s.IncreaseEntryId, s.DecreaseEntryId, s.Amount })
            .ToListAsync(ct);
        var settled = new Dictionary<Guid, decimal>();
        foreach (var s in settlements)
        {
            settled[s.IncreaseEntryId] = settled.GetValueOrDefault(s.IncreaseEntryId) + s.Amount;
            settled[s.DecreaseEntryId] = settled.GetValueOrDefault(s.DecreaseEntryId) + s.Amount;
        }

        return entries.Select(e => new DebtPositionItem
        {
            EntryId = e.Id,
            PartnerId = e.PartnerId,
            Direction = e.Direction,
            SourceType = e.SourceType,
            SourceId = e.SourceId,
            DocCode = e.DocCode,
            DocDate = e.DocDate,
            DueDate = e.DueDate,
            OwnerUserId = e.OwnerUserId,
            Description = e.Description,
            Amount = e.Amount,
            Settled = settled.GetValueOrDefault(e.Id),
        }).ToList();
    }
}
