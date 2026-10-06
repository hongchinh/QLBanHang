using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockReasons.Models;

public class StockReasonDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public StockDirection Direction { get; set; }
    public PartnerType PartnerType { get; set; }
    public bool IsSystem { get; set; }
}

public class CreateStockReasonRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public StockDirection Direction { get; set; }
    public PartnerType PartnerType { get; set; }
}

public class UpdateStockReasonRequest
{
    public string Name { get; set; } = default!;
    public StockDirection Direction { get; set; }
    public PartnerType PartnerType { get; set; }
}
