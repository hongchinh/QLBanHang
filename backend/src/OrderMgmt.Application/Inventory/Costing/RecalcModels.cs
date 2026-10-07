namespace OrderMgmt.Application.Inventory.Costing;

public class RecalculateCostRequest
{
    public DateOnly FromPeriodStart { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ProductId { get; set; }
}

public class RecalculateCostResult
{
    public DateOnly FromPeriodStart { get; set; }
    public int ScopeCount { get; set; }
}
