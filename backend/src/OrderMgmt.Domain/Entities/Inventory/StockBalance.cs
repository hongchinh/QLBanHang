namespace OrderMgmt.Domain.Entities.Inventory;

/// Current quantity on hand of a product in a warehouse (= Σ ledger), maintained on every ledger change (D21).
public class StockBalance
{
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public decimal Quantity { get; set; }
}
