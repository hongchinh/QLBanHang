namespace OrderMgmt.Application.Inventory.OpeningStocks.Models;

// Every member is a public auto-property: System.Text.Json ignores public fields (review m2).
public class OpeningStockGridDto
{
    public Guid WarehouseId { get; set; }
    public DateOnly? OpeningDate { get; set; }
    public List<OpeningStockLineDto> Lines { get; set; } = new();
}

public class OpeningStockLineDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class SaveOpeningStockRequest
{
    public Guid WarehouseId { get; set; }
    public DateOnly OpeningDate { get; set; }
    public bool AcknowledgeNegativeStock { get; set; }
    public List<SaveOpeningStockLine> Lines { get; set; } = new();
}

public class SaveOpeningStockLine
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}
