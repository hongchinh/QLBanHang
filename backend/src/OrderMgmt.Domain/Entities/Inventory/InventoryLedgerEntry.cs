using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Inventory;

/// One stock movement of a product in a warehouse. Derived data: rewritten whenever its source changes.
/// Posting order: PostedAt, SourceType, SourceCode, LineSortOrder, Id.
public class InventoryLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset PostedAt { get; set; }
    public LedgerSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid? SourceLineId { get; set; }
    public string SourceCode { get; set; } = default!;
    public int LineSortOrder { get; set; }

    public Guid BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }

    public decimal QtyIn { get; set; }
    public decimal QtyOut { get; set; }
    public decimal InValue { get; set; }

    /// Quantity on hand of (ProductId, WarehouseId) after this row.
    public decimal RunningQty { get; set; }

    /// Set on outbound rows by the costing run; null on inbound rows.
    public decimal? UnitCost { get; set; }
    public decimal? CostAmount { get; set; }
}
