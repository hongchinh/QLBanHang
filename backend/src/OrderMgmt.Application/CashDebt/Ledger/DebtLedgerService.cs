using OrderMgmt.Application.Common;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Common;
using OrderMgmt.Application.Common.Interfaces;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Organization.Branches;
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
    private readonly SettlementChangeCollector _changes;

    public DebtLedgerService(IAppDbContext db, IDebtActivityLog activityLog, ICurrentUser currentUser, IDateTime clock,
        SettlementChangeCollector changes)
    {
        _db = db;
        _activityLog = activityLog;
        _currentUser = currentUser;
        _clock = clock;
        _changes = changes;
    }

    public async Task<IReadOnlyList<DebtEntry>> PostAsync(DebtSourceType sourceType, Guid sourceId,
        IReadOnlyList<DebtEntryDraft> drafts, bool confirmRemoveSettlements, string? confirmedSettlements = null,
        CancellationToken ct = default)
    {
        if (drafts.GroupBy(d => d.Side).Any(g => g.Count() > 1))
            throw new ArgumentException("A source has at most one debt entry draft per side.", nameof(drafts));

        var (existing, settlements) = await LoadSourceAsync(sourceType, sourceId, ct);
        var existingIds = existing.Select(e => e.Id).ToList();

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
        if (pending.Count > 0
            && (!confirmRemoveSettlements
                || !SettlementRemovalConfirmation.Covers(confirmedSettlements, pending.Select(s => s.Id))))
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
            // A zero draft was checked as a shrink to 0 above; the entry then goes like a removed side.
            if (draft is null || draft.Amount == 0)
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

    public async Task RemoveSourceAsync(DebtSourceType sourceType, Guid sourceId, bool confirmRemoveSettlements,
        string? confirmedSettlements = null, CancellationToken ct = default)
    {
        var (existing, settlements) = await LoadSourceAsync(sourceType, sourceId, ct);
        if (existing.Count == 0)
            return;

        var entries = await WithCounterpartsAsync(existing, settlements, ct);
        if (settlements.Count > 0
            && (!confirmRemoveSettlements
                || !SettlementRemovalConfirmation.Covers(confirmedSettlements, settlements.Select(s => s.Id))))
            throw SettlementsWillBeRemoved(settlements, existing.Select(e => e.Id).ToList(), entries);

        await RemoveCoreAsync(settlements, entries, ct);
        _db.DebtEntries.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveOwnSettlementsAsync(DebtSourceType sourceType, Guid sourceId, SettlementOrigin origin,
        CancellationToken ct = default)
    {
        var (_, settlements) = await LoadSourceAsync(sourceType, sourceId, ct);
        var own = settlements.Where(s => s.Origin == origin && s.BatchId == sourceId).ToList();
        if (own.Count == 0)
            return;

        _db.DebtSettlements.RemoveRange(own);
        RecordRemoved(own);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DebtSettlement>> AddSettlementsAsync(IReadOnlyList<SettlementDraft> drafts,
        SettlementOrigin origin, Guid batchId, string errorKeyPrefix, CancellationToken ct = default)
    {
        if (drafts.Count == 0)
            return Array.Empty<DebtSettlement>();

        var ids = drafts.SelectMany(d => new[] { d.IncreaseEntryId, d.DecreaseEntryId }).Distinct().ToList();
        var entries = await _db.DebtEntries.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        // 1. Pairs and amounts, collecting every error. Open = Amount − existing settlements − earlier items.
        var settled = (await SettledAmountsAsync(entries.Keys, ct: ct)).ToDictionary(x => x.Key, x => x.Value);
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            var key = $"{errorKeyPrefix}[{i}]";
            var increase = entries.GetValueOrDefault(draft.IncreaseEntryId);
            var decrease = entries.GetValueOrDefault(draft.DecreaseEntryId);
            var pairError = PairError(increase, decrease);
            if (pairError is { } pe)
                errors[$"{key}.{pe.Field}"] = new[] { pe.Message };

            if (draft.Amount <= 0)
            {
                errors[$"{key}.amount"] = new[] { "Số tiền đối trừ phải lớn hơn 0." };
                continue;
            }

            if (pairError is not null)
                continue;

            if (settled[increase!.Id] + draft.Amount > increase.Amount || settled[decrease!.Id] + draft.Amount > decrease.Amount)
            {
                errors[$"{key}.amount"] = new[] { "Số tiền đối trừ vượt quá số còn lại của chứng từ." };
                continue;
            }

            settled[increase.Id] += draft.Amount;
            settled[decrease.Id] += draft.Amount;
        }

        if (errors.Count > 0)
            throw new ValidationDomainException(errors, null);

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

        // 2. Period lock on EffectiveAt; LockedUntil is read once for the branches involved.
        var branchIds = entries.Values.Select(e => e.BranchId).Distinct().ToList();
        var lockedUntil = await _db.Branches.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(b => branchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.LockedUntil, ct);
        foreach (var group in added.GroupBy(s => entries[s.IncreaseEntryId].BranchId))
            PeriodLockGuard.EnsureNotLocked(lockedUntil.GetValueOrDefault(group.Key), group.Select(s => s.EffectiveAt));

        _db.DebtSettlements.AddRange(added);
        await _db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<IReadOnlyList<RemovedSettlement>> RemoveSettlementsAsync(IReadOnlyCollection<Guid> settlementIds,
        CancellationToken ct = default)
    {
        var ids = settlementIds.Distinct().ToList();
        var settlements = await _db.DebtSettlements.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var missing = ids.Except(settlements.Select(s => s.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException(nameof(DebtSettlement), missing[0]);

        var entries = await WithCounterpartsAsync(Array.Empty<DebtEntry>(), settlements, ct);
        var removed = await RemoveCoreAsync(settlements, entries, ct);
        await _db.SaveChangesAsync(ct);
        return removed;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> SettledAmountsAsync(IEnumerable<Guid> entryIds,
        SettlementOrigin? excludeOrigin = null, CancellationToken ct = default)
    {
        var ids = entryIds.Distinct().ToList();
        var query = _db.DebtSettlements.Where(s => ids.Contains(s.IncreaseEntryId) || ids.Contains(s.DecreaseEntryId));
        if (excludeOrigin is { } excluded)
            query = query.Where(s => s.Origin != excluded);
        var rows = await query.Select(s => new { s.IncreaseEntryId, s.DecreaseEntryId, s.Amount }).ToListAsync(ct);

        // Every requested id is present; an entry without settlements has 0.
        var result = ids.ToDictionary(id => id, _ => 0m);
        foreach (var row in rows)
        {
            if (result.ContainsKey(row.IncreaseEntryId))
                result[row.IncreaseEntryId] += row.Amount;
            if (result.ContainsKey(row.DecreaseEntryId))
                result[row.DecreaseEntryId] += row.Amount;
        }

        return result;
    }

    /// The first slot holds an Increase entry and the second a Decrease entry of the same branch, partner and side.
    private static (string Field, string Message)? PairError(DebtEntry? increase, DebtEntry? decrease)
    {
        if (increase is null || increase.Direction != DebtDirection.Increase)
            return ("increaseEntryId", "Chứng từ ghi tăng công nợ không hợp lệ.");
        if (decrease is null || decrease.Direction != DebtDirection.Decrease)
            return ("decreaseEntryId", "Chứng từ ghi giảm công nợ không hợp lệ.");
        if (decrease.BranchId != increase.BranchId || decrease.PartnerId != increase.PartnerId || decrease.Side != increase.Side)
            return ("decreaseEntryId", "Hai chứng từ phải cùng chi nhánh, đối tượng và loại công nợ.");
        return null;
    }

    /// The source's entries and every settlement touching them (tracked).
    private async Task<(List<DebtEntry> Entries, List<DebtSettlement> Settlements)> LoadSourceAsync(
        DebtSourceType sourceType, Guid sourceId, CancellationToken ct)
    {
        var entries = await _db.DebtEntries
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
            .ToListAsync(ct);
        var ids = entries.Select(e => e.Id).ToList();
        var settlements = await _db.DebtSettlements
            .Where(s => ids.Contains(s.IncreaseEntryId) || ids.Contains(s.DecreaseEntryId))
            .ToListAsync(ct);
        return (entries, settlements);
    }

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

        RecordRemoved(settlements);

        return removed;
    }

    /// Notes the entries of a removed settlement for AutoRecomputeHook. An Auto settlement is the recompute's own work,
    /// so it never feeds it again.
    private void RecordRemoved(IEnumerable<DebtSettlement> settlements)
    {
        foreach (var s in settlements.Where(s => s.Origin != SettlementOrigin.Auto))
            _changes.Record(s.IncreaseEntryId, s.DecreaseEntryId);
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
        var settlementList = settlements.ToList();
        var details = settlementList
            .Select(s =>
            {
                var sourceIsIncrease = sourceEntryIds.Contains(s.IncreaseEntryId);
                var mine = entries[sourceIsIncrease ? s.IncreaseEntryId : s.DecreaseEntryId];
                var other = entries[sourceIsIncrease ? s.DecreaseEntryId : s.IncreaseEntryId];
                return (Key: other.DocCode, Text: DebtActivityLog.SettlementRemovedText(s.Amount, mine.DocCode));
            })
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Text).ToArray());

        return SettlementRemovalConfirmation.Create(details, settlementList.Select(s => s.Id));
    }

    private static bool Touches(DebtSettlement s, Guid entryId) =>
        s.IncreaseEntryId == entryId || s.DecreaseEntryId == entryId;
}
