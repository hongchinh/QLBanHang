using OrderMgmt.Domain.Entities.CashDebt;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// Writes debt entries and settlements. Callers run inside ITransactionRunner and hold the branch gate and
/// partner locks (P14).
/// Every removal of a non-Auto settlement is recorded in SettlementChangeCollector, and AutoRecomputeHook recomputes the
/// Auto settlements of the stock vouchers involved just before the commit, so callers do not.
/// Only the outermost ITransactionRunner.RunAsync runs the before-commit hooks: a ledger write under RunSnapshotAsync or
/// in a transaction opened outside the runner skips the Auto recompute.
public interface IDebtLedgerService
{
    /// Save path (P21, P11). At most one draft per side (else ArgumentException). Upserts in place by
    /// (SourceType, SourceId, Side); a side missing from drafts is deleted, and settlements on it give 409. A draft with
    /// Amount 0 also deletes the side, but as a shrink to 0: its settlements ask for confirmation (P10) instead.
    Task<IReadOnlyList<DebtEntry>> PostAsync(DebtSourceType sourceType, Guid sourceId,
        IReadOnlyList<DebtEntryDraft> drafts, bool confirmRemoveSettlements, CancellationToken ct = default);

    /// Cancel/delete path: removes every entry of the source after removing its settlements (needs confirmation).
    Task RemoveSourceAsync(DebtSourceType sourceType, Guid sourceId, bool confirmRemoveSettlements,
        CancellationToken ct = default);

    /// Silently removes the source's own settlements of one origin (no confirmation, no activity). A settlement is owned
    /// by the document whose id is its BatchId: Auto by the stock voucher, AtVoucher by the cash voucher that made the
    /// allocation, Offset by the offset voucher. Another document's settlement of the same origin on the source's
    /// entries stays, and is subject to the P10/P11 rules of PostAsync.
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
}
