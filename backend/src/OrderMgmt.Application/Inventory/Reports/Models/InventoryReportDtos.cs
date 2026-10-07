namespace OrderMgmt.Application.Inventory.Reports.Models;

// Every member is a public auto-property: System.Text.Json ignores public fields (review m2).
public class StockOnHandReportRequest
{
    /// Point in time; omitted = now, quantities from StockBalances (D21).
    public DateTimeOffset? At { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ProductGroupId { get; set; }
    public string? Search { get; set; }
}

public class StockOnHandReportDto
{
    public DateTimeOffset At { get; set; }
    /// The costing period containing the VN date of At has not ended yet.
    public bool IsProvisional { get; set; }
    public bool CanViewCost { get; set; }
    public decimal? TotalValue { get; set; }
    public List<StockOnHandRowDto> Rows { get; set; } = new();
}

public class StockOnHandRowDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? ProductGroupName { get; set; }
    public string UnitName { get; set; } = "";
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? Value { get; set; }
}
