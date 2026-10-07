namespace OrderMgmt.Domain.Entities.Inventory;

/// Output of a costing run for one period of (ProductId, ScopeKey).
/// ScopeKey is the branch id (Branch scope) or the warehouse id (Warehouse scope).
public class InventoryCostPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ScopeKey { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal OpeningQty { get; set; }
    public decimal OpeningValue { get; set; }
    public decimal InQty { get; set; }
    public decimal InValue { get; set; }
    public decimal OutQty { get; set; }
    public decimal OutValue { get; set; }
    public decimal AvgCost { get; set; }
    public decimal ClosingQty { get; set; }
    public decimal ClosingValue { get; set; }
}
