using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Ledger;

public interface IInventoryLedgerService
{
    /// Replaces every ledger row of (sourceType, sourceId) with `entries`, recomputes RunningQty and StockBalance
    /// of the affected (product, warehouse) pairs. Runs inside the caller's transaction.
    Task<LedgerChangeResult> ReplaceSourceAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, CancellationToken ct = default);
}
