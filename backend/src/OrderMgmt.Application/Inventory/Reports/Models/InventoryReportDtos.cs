using OrderMgmt.Domain.Enums;

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

public class StockCardRequest
{
    public Guid ProductId { get; set; }
    /// Null = every warehouse of the working branch.
    public Guid? WarehouseId { get; set; }
    /// VN dates, both required, From <= To.
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class StockCardDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    /// The costing period containing To has not ended yet.
    public bool IsProvisional { get; set; }
    public bool CanViewCost { get; set; }
    /// D32: a warehouse filter under Branch scope has no meaningful running value (opening/running/closing values null).
    public bool ValuesAtScopeOnly { get; set; }
    public decimal OpeningQty { get; set; }
    public decimal? OpeningValue { get; set; }
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public decimal? InValue { get; set; }
    public decimal? OutValue { get; set; }
    public decimal ClosingQty { get; set; }
    public decimal? ClosingValue { get; set; }
    public List<StockCardRowDto> Rows { get; set; } = new();
}

public class StockCardRowDto
{
    public DateTimeOffset PostedAt { get; set; }
    public LedgerSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceCode { get; set; } = "";
    public string? ReasonName { get; set; }
    public string? PartnerName { get; set; }
    public string WarehouseCode { get; set; } = "";
    public decimal QtyIn { get; set; }
    public decimal QtyOut { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? InValue { get; set; }
    public decimal? CostAmount { get; set; }
    public decimal RunningQty { get; set; }
    public decimal? RunningValue { get; set; }
}
