using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Posting;

public interface IInventoryPostingService
{
    // Shared branch gate first, then one key per product, in ascending key order (D30).
    Task AcquireLocksAsync(Guid branchId, IEnumerable<Guid> productIds, CancellationToken ct = default);

    // ReplaceSourceAsync → negative-stock policy → ScopesForAsync + RecalculateAsync.
    // Must be called inside ITransactionRunner after AcquireLocksAsync.
    Task<LedgerChangeResult> PostAsync(LedgerSourceType sourceType, Guid sourceId,
        IReadOnlyList<LedgerEntryDraft> entries, bool acknowledgeNegativeStock, CancellationToken ct = default);
}
