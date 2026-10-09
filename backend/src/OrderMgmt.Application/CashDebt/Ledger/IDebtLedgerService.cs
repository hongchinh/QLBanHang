using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// Writes debt entries and settlements. Callers run inside ITransactionRunner and hold the branch gate and
/// partner locks (P14).
public interface IDebtLedgerService
{
    /// Save path (P21, P11). At most one draft per side (else ArgumentException). Upserts in place by
    /// (SourceType, SourceId, Side); a side missing from drafts is deleted.
    Task<IReadOnlyList<DebtEntry>> PostAsync(DebtSourceType sourceType, Guid sourceId,
        IReadOnlyList<DebtEntryDraft> drafts, bool confirmRemoveSettlements, CancellationToken ct = default);

    /// Cancel/delete path: removes every entry of the source after removing its settlements (needs confirmation).
    Task RemoveSourceAsync(DebtSourceType sourceType, Guid sourceId, bool confirmRemoveSettlements,
        CancellationToken ct = default);

    /// Silently removes settlements of one origin attached to the source's entries (no confirmation, no activity).
    Task RemoveOwnSettlementsAsync(DebtSourceType sourceType, Guid sourceId, SettlementOrigin origin,
        CancellationToken ct = default);

    /// Validates and inserts settlements. Validation errors use keys "{errorKeyPrefix}[i].amount" / "…increaseEntryId" / "…decreaseEntryId".
    Task<IReadOnlyList<DebtSettlement>> AddSettlementsAsync(IReadOnlyList<SettlementDraft> drafts, SettlementOrigin origin,
        Guid batchId, string errorKeyPrefix, CancellationToken ct = default);

    /// Removes settlements and logs SettlementRemoved on both documents.
    Task<IReadOnlyList<RemovedSettlement>> RemoveSettlementsAsync(IReadOnlyCollection<Guid> settlementIds,
        CancellationToken ct = default);

    /// Σ settlement amounts per entry, optionally excluding one origin.
    Task<IReadOnlyDictionary<Guid, decimal>> SettledAmountsAsync(IEnumerable<Guid> entryIds,
        SettlementOrigin? excludeOrigin = null, CancellationToken ct = default);

    /// Ids of the entries settled against the source's entries (the other entry of each settlement). A write that may
    /// remove those settlements reads them first, then passes them to IAutoCashVoucherService.RecomputeAutoAsync.
    Task<IReadOnlyList<Guid>> CounterpartEntryIdsAsync(DebtSourceType sourceType, Guid sourceId,
        CancellationToken ct = default);
}
