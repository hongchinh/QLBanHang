namespace OrderMgmt.Application.Dashboards.Executive.Models;

public class InventorySummaryDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool CanViewCost { get; set; }

    /// Now; null without inventory.view_cost.
    public decimal? StockValue { get; set; }

    /// Products with a non-zero quantity.
    public int ProductCount { get; set; }

    /// Warehouses holding stock.
    public int WarehouseCount { get; set; }

    /// Product × warehouse pairs below zero.
    public int NegativeCount { get; set; }

    /// Period: value received by stock-in vouchers.
    public decimal? InValue { get; set; }

    /// Period: cost issued by stock-out vouchers.
    public decimal? OutCost { get; set; }
}

public class InventoryBreakdownDto
{
    public bool CanViewCost { get; set; }
    public List<StockGroupDto> ByWarehouse { get; set; } = new();
    public List<StockGroupDto> ByProductGroup { get; set; } = new();
    public List<NegativeStockRowDto> Negative { get; set; } = new();
    public List<StockProductRowDto> TopByValue { get; set; } = new();
}

public class StockGroupDto
{
    /// Warehouse id or product group id; null: products without a group.
    public Guid? Id { get; set; }

    /// Warehouse code; null for product groups.
    public string? Code { get; set; }
    public string? Name { get; set; }

    /// Warehouses only.
    public string? BranchCode { get; set; }
    public int ProductCount { get; set; }
    public decimal? Value { get; set; }
}

public class NegativeStockRowDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class StockProductRowDto
{
    public Guid ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Value { get; set; }
}
