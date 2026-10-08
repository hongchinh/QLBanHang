using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

public class DebtLedgerService : IDebtLedgerService
{
    private static readonly DebtSide[] Sides = { DebtSide.Receivable, DebtSide.Payable };

    private readonly IAppDbContext _db;
    private readonly IDebtActivityLog _activityLog;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;

    public DebtLedgerService(IAppDbContext db, IDebtActivityLog activityLog, ICurrentUser currentUser, IDateTime clock)
    {
        _db = db;
        _activityLog = activityLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DebtEntry>> PostAsync(DebtSourceType sourceType, Guid sourceId,
        IReadOnlyList<DebtEntryDraft> drafts, bool confirmRemoveSettlements, CancellationToken ct = default)
    {
        if (drafts.GroupBy(d => d.Side).Any(g => g.Count() > 1))
            throw new ArgumentException("A source has at most one debt entry draft per side.", nameof(drafts));

        var existing = await _db.DebtEntries
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
            .ToListAsync(ct);
        var existingIds = existing.Select(e => e.Id).ToList();
        var settlements = await _db.DebtSettlements
            .Where(s => existingIds.Contains(s.IncreaseEntryId) || existingIds.Contains(s.DecreaseEntryId))
            .ToListAsync(ct);

        // 1. Evaluate both sides before changing anything (P11), collecting the settlements a shrink would drop.
        var pending = new List<DebtSettlement>();
        foreach (var entry in existing)
        {
            var own = settlements.Where(s => Touches(s, entry.Id)).ToList();
            var settled = own.Sum(s => s.Amount);
            if (settled == 0)
                continue;

            var draft = drafts.SingleOrDefault(d => d.Side == entry.Side);
            if (draft is null
                || draft.BranchId != entry.BranchId
                || draft.PartnerId != entry.PartnerId
                || draft.Direction != entry.Direction)
                throw new ConflictException("DEBT_HAS_SETTLEMENTS", "Chứng từ đã được đối trừ, hãy bỏ đối trừ trước.");

            if (draft.Amount < settled)
                pending.AddRange(own);
        }

        var entries = await WithCounterpartsAsync(existing, settlements, ct);
        if (pending.Count > 0 && !confirmRemoveSettlements)
            throw SettlementsWillBeRemoved(pending, existingIds, entries);

        // 2. Apply: drop the confirmed settlements, then upsert or delete each side in place.
        await RemoveCoreAsync(pending, entries, ct);
        var remaining = settlements.Except(pending).ToList();
        var now = _clock.UtcNow;
        var result = new List<DebtEntry>();
        foreach (var side in Sides)
        {
            var entry = existing.SingleOrDefault(e => e.Side == side);
            var draft = drafts.SingleOrDefault(d => d.Side == side);
            if (draft is null)
            {
                if (entry is not null)
                    _db.DebtEntries.Remove(entry);
                continue;
            }

            if (entry is null)
            {
                entry = new DebtEntry { SourceType = sourceType, SourceId = sourceId, Side = side };
                _db.DebtEntries.Add(entry);
            }

            var postedAt = VnTime.ToUtc(draft.PostedAt);
            var postedAtChanged = entry.PostedAt != postedAt;
            entry.BranchId = draft.BranchId;
            entry.PartnerId = draft.PartnerId;
            entry.Direction = draft.Direction;
            entry.Amount = draft.Amount;
            entry.PostedAt = postedAt;
            entry.DocDate = draft.DocDate;
            entry.DueDate = draft.DueDate;
            entry.OwnerUserId = draft.OwnerUserId;
            entry.DocCode = draft.DocCode;
            entry.Description = draft.Description;
            entry.UpdatedAt = now;

            if (postedAtChanged)
            {
                foreach (var s in remaining.Where(s => Touches(s, entry.Id)))
                    s.EffectiveAt = DebtDates.EffectiveAt(entries[s.IncreaseEntryId].PostedAt, entries[s.DecreaseEntryId].PostedAt);
            }

            result.Add(entry);
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    public Task RemoveSourceAsync(DebtSourceType sourceType, Guid sourceId, bool confirmRemoveSettlements,
        CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Phase 04 Task 4.4.");

    public Task RemoveOwnSettlementsAsync(DebtSourceType sourceType, Guid sourceId, SettlementOrigin origin,
        CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Phase 04 Task 4.4.");

    /// Inserts only for now; the validation rules come with Phase 04 Task 4.4.
    public async Task<IReadOnlyList<DebtSettlement>> AddSettlementsAsync(IReadOnlyList<SettlementDraft> drafts,
        SettlementOrigin origin, Guid batchId, string errorKeyPrefix, CancellationToken ct = default)
    {
        var ids = drafts.SelectMany(d => new[] { d.IncreaseEntryId, d.DecreaseEntryId }).Distinct().ToList();
        var entries = await _db.DebtEntries.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        var now = _clock.UtcNow;
        var added = drafts.Select(d => new DebtSettlement
        {
            IncreaseEntryId = d.IncreaseEntryId,
            DecreaseEntryId = d.DecreaseEntryId,
            Amount = d.Amount,
            Origin = origin,
            BatchId = batchId,
            EffectiveAt = DebtDates.EffectiveAt(entries[d.IncreaseEntryId].PostedAt, entries[d.DecreaseEntryId].PostedAt),
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        }).ToList();
        _db.DebtSettlements.AddRange(added);
        await _db.SaveChangesAsync(ct);
        return added;
    }

    public Task<IReadOnlyList<RemovedSettlement>> RemoveSettlementsAsync(IReadOnlyCollection<Guid> settlementIds,
        CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Phase 04 Task 4.4.");

    public Task<IReadOnlyDictionary<Guid, decimal>> SettledAmountsAsync(IEnumerable<Guid> entryIds,
        SettlementOrigin? excludeOrigin = null, CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Phase 04 Task 4.4.");

    /// Removes settlements (no save) and logs SettlementRemoved on the documents of both entries.
    /// `entries` must hold both entries of every settlement.
    private async Task<IReadOnlyList<RemovedSettlement>> RemoveCoreAsync(IReadOnlyCollection<DebtSettlement> settlements,
        IReadOnlyDictionary<Guid, DebtEntry> entries, CancellationToken ct)
    {
        var removed = new List<RemovedSettlement>(settlements.Count);
        foreach (var s in settlements)
        {
            var increase = entries[s.IncreaseEntryId];
            var decrease = entries[s.DecreaseEntryId];
            await _activityLog.SettlementRemovedAsync(increase, decrease, s.Amount, ct);
            await _activityLog.SettlementRemovedAsync(decrease, increase, s.Amount, ct);
            _db.DebtSettlements.Remove(s);
            removed.Add(new RemovedSettlement(s.Id, s.IncreaseEntryId, s.DecreaseEntryId, s.Amount, s.Origin));
        }

        return removed;
    }

    /// `entries` keyed by id, plus the other entry of every settlement.
    private async Task<Dictionary<Guid, DebtEntry>> WithCounterpartsAsync(IReadOnlyList<DebtEntry> entries,
        IReadOnlyList<DebtSettlement> settlements, CancellationToken ct)
    {
        var byId = entries.ToDictionary(e => e.Id);
        var missing = settlements.SelectMany(s => new[] { s.IncreaseEntryId, s.DecreaseEntryId })
            .Where(id => !byId.ContainsKey(id))
            .Distinct()
            .ToList();
        if (missing.Count > 0)
        {
            foreach (var other in await _db.DebtEntries.Where(e => missing.Contains(e.Id)).ToListAsync(ct))
                byId[other.Id] = other;
        }

        return byId;
    }

    /// 422 DEBT_SETTLEMENTS_WILL_BE_REMOVED (P10). Details: key = the other entry's DocCode,
    /// value = "Bỏ đối trừ {amount} với {source entry's DocCode}".
    private static ConfirmationRequiredException SettlementsWillBeRemoved(IEnumerable<DebtSettlement> settlements,
        IReadOnlyCollection<Guid> sourceEntryIds, IReadOnlyDictionary<Guid, DebtEntry> entries)
    {
        var details = settlements
            .Select(s =>
            {
                var sourceIsIncrease = sourceEntryIds.Contains(s.IncreaseEntryId);
                var mine = entries[sourceIsIncrease ? s.IncreaseEntryId : s.DecreaseEntryId];
                var other = entries[sourceIsIncrease ? s.DecreaseEntryId : s.IncreaseEntryId];
                return (Key: other.DocCode, Text: DebtActivityLog.SettlementRemovedText(s.Amount, mine.DocCode));
            })
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Text).ToArray());

        return new ConfirmationRequiredException("DEBT_SETTLEMENTS_WILL_BE_REMOVED",
            "Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau.", details, canConfirm: true);
    }

    private static bool Touches(DebtSettlement s, Guid entryId) =>
        s.IncreaseEntryId == entryId || s.DecreaseEntryId == entryId;
}
