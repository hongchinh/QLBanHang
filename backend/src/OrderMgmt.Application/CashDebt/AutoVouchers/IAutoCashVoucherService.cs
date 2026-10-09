using OrderMgmt.Domain.Entities.Inventory;

namespace OrderMgmt.Application.CashDebt.AutoVouchers;

/// The receipt or payment a stock voucher manages for its paid amount (P20, P3).
public interface IAutoCashVoucherService
{
    /// Brings the managed receipt/payment of `voucher` in line with its current state. Call after the stock voucher's
    /// own debt entry is posted or removed, inside the stock voucher's transaction and locks (branch gate, partner and
    /// fund keys of the old and new state); takes only the cash counter row. Uses DbSet.Add for new rows; saves.
    /// - Active, not deleted, PaidAmount > 0: creates the voucher (type = DebtEffectRules.AutoVoucherType, new code) or
    ///   updates the existing one (status → Active, code kept); posts its entry (the stock reason's side, opposite
    ///   direction, Amount = PaidAmount) when reason.DebtEffect ≠ None, else none; then adds the Auto settlement
    ///   (batch = voucher.Id) of DebtSettlementMath.AutoAmount when it is > 0.
    /// - Active, PaidAmount = 0, or deleted: removes its entry (confirmation per P10) and soft-deletes it; its number
    ///   is not reused.
    /// - Cancelled: removes its entry (confirmation per P10) and cancels it.
    /// Logs a CashDocumentActivity on the automatic voucher and, when it is created or deleted, AutoVoucherChanged on
    /// the stock voucher.
    Task SyncAsync(StockVoucher voucher, StockReason reason, bool confirmRemoveSettlements, CancellationToken ct = default);

    /// Sets the Auto settlement of every non-deleted, Active stock voucher with PaidAmount > 0 whose own entry or
    /// automatic voucher's entry is among `entryIds` (both entries existing) to DebtSettlementMath.AutoAmount of the
    /// current rows (none when 0), replacing it in place (batch = the stock voucher); writes no activity.
    /// An Auto settlement dated on or before the branch's LockedUntil is left unchanged (user decision 2026-10-09,
    /// brainstorm section-02 §5): the open-period write goes through, and the stock voucher's next save after
    /// unlocking recomputes it.
    /// A write that may remove settlements reads the entries they join first (the ledger's CounterpartEntryIdsAsync)
    /// and calls this after its own new settlements, so Auto takes only what is left; saves.
    /// Locks: a settlement joins entries of one branch, partner and side, so the partner key the caller holds for the
    /// removed settlements covers these stock vouchers too. Settlements move no money, so no fund key is needed.
    Task RecomputeAutoAsync(IEnumerable<Guid> entryIds, CancellationToken ct = default);
}
