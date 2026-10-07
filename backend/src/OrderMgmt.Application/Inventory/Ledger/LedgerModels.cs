using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.Ledger;

public sealed record LedgerEntryDraft(
    Guid ProductId, Guid WarehouseId, Guid BranchId, DateTimeOffset PostedAt,
    LedgerSourceType SourceType, Guid SourceId, Guid? SourceLineId, string SourceCode, int LineSortOrder,
    decimal QtyIn, decimal QtyOut, decimal InValue);

public sealed record PairChange(
    Guid ProductId, Guid WarehouseId, Guid BranchId,
    DateTimeOffset From,                // earliest old/new PostedAt of this pair (UTC)
    decimal? MinRunningQty,             // AFTER the change: min RunningQty among rows with PostedAt >= From (null if none)
    DateTimeOffset? FirstNegativeAt,    // AFTER: PostedAt of the first such row with RunningQty < 0
    decimal? OldMinRunningQty,          // BEFORE the change, same window (read before the old rows are deleted; D31)
    DateTimeOffset? OldFirstNegativeAt, // BEFORE: first negative point in the same window
    decimal Balance,                    // StockBalance after the change
    decimal? BaseRunningQty);           // RunningQty of the last row before From (same before and after; null if none)

public sealed record LedgerChangeResult(IReadOnlyList<PairChange> Pairs);

public static class LedgerQueryExtensions
{
    /// Posting order (D34): PostedAt, SourceType, SourceCode, LineSortOrder, Id. Always ordered by the database.
    public static IQueryable<InventoryLedgerEntry> InPostingOrder(this IQueryable<InventoryLedgerEntry> rows) =>
        rows.OrderBy(e => e.PostedAt)
            .ThenBy(e => e.SourceType)
            .ThenBy(e => e.SourceCode)
            .ThenBy(e => e.LineSortOrder)
            .ThenBy(e => e.Id);

    public static IQueryable<InventoryLedgerEntry> InReversePostingOrder(this IQueryable<InventoryLedgerEntry> rows) =>
        rows.OrderByDescending(e => e.PostedAt)
            .ThenByDescending(e => e.SourceType)
            .ThenByDescending(e => e.SourceCode)
            .ThenByDescending(e => e.LineSortOrder)
            .ThenByDescending(e => e.Id);
}
